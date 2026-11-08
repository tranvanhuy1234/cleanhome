using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

internal static class CatalogChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, bool detailOnly = false)
    {
        var settings = new MySqlConnectionStringBuilder(connectionString);
        if (!Regex.IsMatch(settings.Database, "^cleanhome_test_[a-f0-9]{12}_service$"))
            throw new InvalidOperationException("Catalog fixtures require an isolated QA database.");
        var empty = await GetAsync(http, "/api/catalog/services", 200);
        Check(empty.GetProperty("total").GetInt64() == 0 && empty.GetProperty("items").GetArrayLength() == 0,
            "empty catalog returns HTTP 200 and an empty page");
        if (detailOnly) await GetAsync(http, "/api/catalog/services/1", 404);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var seed = new MySqlCommand("""
            INSERT INTO service_categories (id,name,description,is_active) VALUES
            (1,'Dọn dẹp','QA category',1),(2,'Nội thất và thiết bị','QA category',1),(3,'Danh mục ẩn','Hidden',0);
            INSERT INTO services (id,category_id,name,description,unit,base_price,duration_minutes,is_active) VALUES
            (1,1,'Dọn nhà','Mô tả tiếng Việt','giờ',150000.11,60,1),
            (2,2,'Vệ sinh sofa','Làm sạch sofa','bộ',300000,90,1),
            (3,1,'Dịch vụ ngừng cung cấp','Hidden','lần',100000,60,0),
            (4,3,'Dịch vụ trong danh mục ẩn','Hidden','lần',100000,60,1),
            (5,1,'Literal_%!','Literal wildcard characters','lần',50000,30,1),
            (6,2,'vệ SINH máy lạnh','Thiết bị','máy',200000,60,1),
            (7,1,'Giặt rèm','needle-description-only','bộ',100000,60,1);
            """, connection)) await seed.ExecuteNonQueryAsync();

        if (detailOnly)
        {
            await CheckDetailAsync(http, connection);
            return;
        }

        // Không khởi động auth-service: chứng minh API catalog là public.
        var page = await GetAsync(http, "/api/catalog/services", 200);
        Check(page.GetProperty("total").GetInt64() == 5 && page.GetProperty("page").GetInt32() == 1 && page.GetProperty("pageSize").GetInt32() == 20,
            "public list filters inactive services and inactive categories");
        var ids = page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64()).ToArray();
        Check(ids.SequenceEqual(new long[] { 7, 6, 5, 2, 1 }), "catalog has stable descending ID ordering");
        var first = page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetInt64() == 1);
        Check(first.GetProperty("categoryId").GetInt64() == 1 && first.GetProperty("categoryName").GetString() == "Dọn dẹp" &&
            first.GetProperty("description").GetString() == "Mô tả tiếng Việt" && first.GetProperty("unit").GetString() == "giờ" &&
            first.GetProperty("basePrice").GetDecimal() == 150000.11m && first.GetProperty("durationMinutes").GetInt32() == 60,
            "category, Vietnamese text, decimal price and duration survive the round trip");
        Check(first.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "basePrice", "categoryId", "categoryName", "description", "durationMinutes", "id", "name", "unit" }),
            "public response contains only the service DTO");

        var found = await GetAsync(http, Search("  VỆ sinh  "), 200);
        Check(found.GetProperty("total").GetInt64() == 2 && found.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).SequenceEqual(new long[] { 6, 2 }), "name search trims whitespace and ignores case");
        foreach (var keyword in new[] { "_%!", "%", "_", "!" })
        {
            found = await GetAsync(http, Search(keyword), 200);
            Check(found.GetProperty("total").GetInt64() == 1 && found.GetProperty("items")[0].GetProperty("id").GetInt64() == 5,
                "LIKE wildcard and escape characters are literal");
        }
        foreach (var keyword in new[] { "missing-service", "' OR 1=1 --", "needle-description-only", "ngừng cung cấp", "danh mục ẩn" })
        {
            found = await GetAsync(http, Search(keyword), 200);
            Check(found.GetProperty("total").GetInt64() == 0 && found.GetProperty("items").GetArrayLength() == 0,
                "unmatched/hidden names and SQL text cannot reveal catalog records");
        }
        found = await GetAsync(http, Search("  "), 200);
        Check(found.GetProperty("total").GetInt64() == 5, "blank search behaves like the full public list");
        for (var number = 1; number <= 4; number++)
        {
            var result = await GetAsync(http, $"/api/catalog/services?page={number}&pageSize=2", 200);
            Check(result.GetProperty("total").GetInt64() == 5 && result.GetProperty("page").GetInt32() == number &&
                result.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())
                    .SequenceEqual(ids.Skip((number - 1) * 2).Take(2)), "pagination retains the filtered total and expected items");
        }
        var filtered = await GetAsync(http, Search("vệ sinh") + "&page=2&pageSize=1", 200);
        Check(filtered.GetProperty("total").GetInt64() == 2 && filtered.GetProperty("items")[0].GetProperty("id").GetInt64() == 2,
            "search and pagination are applied together");
        foreach (var invalid in new[] { "page=0", "page=-1", "page=100001", "page=abc", "pageSize=0", "pageSize=101", "search=" + new string('x', 151) })
            await GetAsync(http, "/api/catalog/services?" + invalid, 400);
        await using (var hide = new MySqlCommand("UPDATE services SET is_active=0", connection)) await hide.ExecuteNonQueryAsync();
        empty = await GetAsync(http, "/api/catalog/services", 200);
        Check(empty.GetProperty("total").GetInt64() == 0 && empty.GetProperty("items").GetArrayLength() == 0,
            "catalog with only inactive services returns an empty result");
        Console.WriteLine("PASS: public catalog list/search through gateway: visibility, DTO, text/price, pagination, literals and input validation.");
    }

    private static async Task CheckDetailAsync(HttpClient http, MySqlConnection connection)
    {
        // Chỉ catalog và gateway đang chạy: GET chi tiết không phụ thuộc đăng nhập.
        var item = await GetAsync(http, "/api/catalog/services/1", 200);
        Check(item.GetProperty("id").GetInt64() == 1 && item.GetProperty("categoryId").GetInt64() == 1 &&
            item.GetProperty("categoryName").GetString() == "Dọn dẹp" && item.GetProperty("name").GetString() == "Dọn nhà" &&
            item.GetProperty("description").GetString() == "Mô tả tiếng Việt" && item.GetProperty("unit").GetString() == "giờ" &&
            item.GetProperty("basePrice").GetDecimal() == 150000.11m && item.GetProperty("durationMinutes").GetInt32() == 60,
            "public service detail preserves category, Vietnamese text, decimal price and duration");
        Check(item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "basePrice", "categoryId", "categoryName", "description", "durationMinutes", "id", "name", "unit" }),
            "detail uses only the public service response DTO");
        var listing = await GetAsync(http, "/api/catalog/services", 200);
        var listed = listing.GetProperty("items").EnumerateArray().Single(service => service.GetProperty("id").GetInt64() == 1);
        Check(listed.GetRawText() == item.GetRawText(), "detail matches the public list contract");
        var hidden = await GetAsync(http, "/api/catalog/services/3", 404);
        await GetAsync(http, "/api/catalog/services/4", 404);
        var missing = await GetAsync(http, $"/api/catalog/services/{long.MaxValue}", 404);
        Check(hidden.GetProperty("title").GetString() == missing.GetProperty("title").GetString(), "hidden and missing services share the same public error");
        await GetAsync(http, "/api/catalog/services/0", 400);
        await GetAsync(http, "/api/catalog/services/-1", 400);
        await GetAsync(http, "/api/catalog/services/not-an-id", 404);
        await GetAsync(http, "/api/catalog/services/9223372036854775808", 404);
        await using (var deactivate = new MySqlCommand("UPDATE services SET is_active=0 WHERE id=1", connection)) await deactivate.ExecuteNonQueryAsync();
        await GetAsync(http, "/api/catalog/services/1", 404);
        await using (var activate = new MySqlCommand("UPDATE services SET is_active=1 WHERE id=1; UPDATE service_categories SET is_active=0 WHERE id=1;", connection))
            await activate.ExecuteNonQueryAsync();
        await GetAsync(http, "/api/catalog/services/1", 404);
        await using (var restore = new MySqlCommand("UPDATE service_categories SET is_active=1 WHERE id=1", connection)) await restore.ExecuteNonQueryAsync();
        var restored = await GetAsync(http, "/api/catalog/services/1", 200);
        Check(restored.GetRawText() == item.GetRawText(), "detail reflects current visibility and does not alter service data");
        Console.WriteLine("PASS: public catalog detail through gateway: DTO, visibility changes, missing/invalid IDs, decimal/text and read-only behavior.");
    }

    private static string Search(string keyword) => "/api/catalog/services?search=" + Uri.EscapeDataString(keyword);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }
    private static async Task<JsonElement> GetAsync(HttpClient http, string route, int expected)
    {
        using var response = await http.GetAsync(route);
        if ((int)response.StatusCode != expected)
            throw new InvalidOperationException($"GET {route}: expected {expected}, got {(int)response.StatusCode}.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
