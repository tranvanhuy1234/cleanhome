using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

internal static class CatalogManagementChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, string admin, string customer, string staff)
    {
        if (!Regex.IsMatch(new MySqlConnectionStringBuilder(connectionString).Database, "^cleanhome_test_[a-f0-9]{12}_service$"))
            throw new InvalidOperationException("Management fixtures require isolated QA database.");
        const string services = "/api/catalog/admin/services";
        const string categories = "/api/catalog/admin/categories";
        foreach (var (method, routePath) in new[]
        {
            ("GET", services),("GET", services+"/1"),("POST", services),("PUT", services+"/1"),("PATCH", services+"/1/status"),
            ("GET", categories),("GET", categories+"/1"),("POST", categories),("PUT", categories+"/1"),("PATCH", categories+"/1/status")
        })
        {
            object? body = method == "GET" ? null : new { };
            await Call(method, routePath, 401, body);
            await Call(method, routePath, 403, body, customer);
            await Call(method, routePath, 403, body, staff);
        }
        Console.WriteLine("PASS: all ten catalog management routes enforce admin authorization.");

        var empty = await Call("GET", services, 200, token: admin);
        Check(empty.GetProperty("total").GetInt64() == 0, "empty management catalog");
        var category = await Call("POST", categories, 201, new { name = "  Vệ sinh nội thất  ", description = "Danh mục QA", isActive = true }, admin);
        var categoryId = category.GetProperty("id").GetInt64();
        Check(category.GetProperty("name").GetString() == "Vệ sinh nội thất", "category trims its name");
        var hiddenCategory = await Call("POST", categories, 201, new { name = "Ẩn QA", description = "Hidden", isActive = false }, admin);
        var hiddenId = hiddenCategory.GetProperty("id").GetInt64();
        var categoryPath = categories + "/" + categoryId;
        await Call("GET", categoryPath, 200, token: admin);
        var item = await Call("POST", services, 201, ServiceBody(categoryId, "Sofa_%! QA"), admin);
        var id = item.GetProperty("id").GetInt64();
        var path = services + "/" + id;
        var publicPath = "/api/catalog/services/" + id;
        var read = await Call("GET", path, 200, token: admin);
        Check(read.GetRawText() == item.GetRawText() && item.GetProperty("isActive").GetBoolean() && item.GetProperty("categoryIsActive").GetBoolean(), "created service can be read with state metadata");
        var visible = await Call("GET", publicPath, 200);
        Check(visible.GetProperty("basePrice").GetDecimal() == 150000.11m && visible.GetProperty("categoryName").GetString() == "Vệ sinh nội thất", "created service is public with exact price");
        var literal = await Call("GET", services + "?search=" + Uri.EscapeDataString("_%!"), 200, token: admin);
        Check(literal.GetProperty("total").GetInt64() == 1, "admin name search handles literal wildcards");
        var inactive = await Call("POST", services, 201, ServiceBody(hiddenId, "Inactive QA", active: false), admin);
        await Call("GET", "/api/catalog/services/" + inactive.GetProperty("id").GetInt64(), 404);
        Console.WriteLine("PASS: category/service creation, public visibility, decimal values and hidden drafts.");

        await Call("POST", services, 404, ServiceBody(long.MaxValue), admin);
        await Call("POST", services, 409, ServiceBody(hiddenId), admin);
        foreach (var invalid in new object[]
        {
            ServiceBody(categoryId, price: -1), ServiceBody(categoryId, price: 1.001m), ServiceBody(categoryId, price: 10000000000m),
            ServiceBody(categoryId, duration: 0), ServiceBody(categoryId, name: " "),
            new { categoryId, name = "Missing fields" },
            new { categoryId, name = "QA", description = "QA", unit = "bộ", basePrice = 10, durationMinutes = 60, isActive = true, role = "admin" }
        }) await Call("POST", services, 400, invalid, admin);
        await Call("POST", categories, 400, new { name = " ", description = "QA", isActive = true }, admin);
        await Call("PATCH", path + "/status", 400, new { }, admin);
        await Call("PATCH", categoryPath + "/status", 400, new { isActive = false, unexpected = true }, admin);
        foreach (var invalid in new[] { "page=0", "pageSize=101", "categoryId=0", "isActive=invalid", "search=" + new string('x', 151) })
            await Call("GET", services + "?" + invalid, 400, token: admin);
        await Call("PUT", services + "/" + long.MaxValue, 404, ServiceBody(categoryId), admin);
        await Call("PUT", categories + "/" + long.MaxValue, 404, new { name = "QA", description = "QA", isActive = true }, admin);
        await Call("GET", services + "/0", 400, token: admin);
        await Call("GET", categories + "/-1", 400, token: admin);
        await Call("GET", services + "/" + long.MaxValue, 404, token: admin);
        await Call("GET", categories + "/" + long.MaxValue, 404, token: admin);
        read = await Call("GET", path, 200, token: admin);
        Check(read.GetRawText() == item.GetRawText(), "invalid writes preserve original record");
        await Call("PUT", path, 409, ServiceBody(hiddenId, "Must not save"), admin);
        read = await Call("GET", path, 200, token: admin);
        Check(read.GetRawText() == item.GetRawText(), "category validation rolls back service update");
        Console.WriteLine("PASS: complete input validation, missing records, rollback and query validation.");

        await Call("PATCH", categoryPath + "/status", 200, new { isActive = false }, admin);
        await Call("GET", publicPath, 404);
        read = await Call("GET", path, 200, token: admin);
        Check(read.GetProperty("isActive").GetBoolean() && !read.GetProperty("categoryIsActive").GetBoolean(), "category hide preserves service's own flag");
        await Call("PATCH", path + "/status", 409, new { isActive = true }, admin);
        await Call("PATCH", path + "/status", 200, new { isActive = false }, admin);
        await Call("PATCH", categoryPath + "/status", 200, new { isActive = true }, admin);
        await Call("GET", publicPath, 404);
        await Call("PATCH", path + "/status", 200, new { isActive = true }, admin);
        await Call("GET", publicPath, 200);
        await Call("PUT", categoryPath, 200, new { name = "Danh mục đã cập nhật", description = "Updated category", isActive = true }, admin);
        item = await Call("PUT", path, 200, ServiceBody(categoryId, "Vệ sinh SOFA mới", 123456.78m, 120), admin);
        Check(item.GetProperty("id").GetInt64() == id && item.GetProperty("categoryName").GetString() == "Danh mục đã cập nhật" && item.GetProperty("durationMinutes").GetInt32() == 120, "update keeps ID and returns latest category metadata");
        var filtered = await Call("GET", services + "?search=sofa&isActive=true&categoryId=" + categoryId + "&pageSize=1", 200, token: admin);
        Check(filtered.GetProperty("total").GetInt64() == 1 && filtered.GetProperty("items")[0].GetProperty("id").GetInt64() == id, "admin filters compose correctly");
        filtered = await Call("GET", services + "?isActive=false", 200, token: admin);
        Check(filtered.GetProperty("total").GetInt64() == 1, "inactive service remains manageable");
        filtered = await Call("GET", categories + "?isActive=false", 200, token: admin);
        Check(filtered.GetProperty("total").GetInt64() == 1, "category filter includes hidden records for admin");
        var secondPage = await Call("GET", services + "?page=2&pageSize=1", 200, token: admin);
        Check(secondPage.GetProperty("total").GetInt64() == 2 && secondPage.GetProperty("items").GetArrayLength() == 1, "admin pagination has filtered total");

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var link = new MySqlCommand("INSERT INTO service_packages (id,name,price,validity_days,is_active) VALUES (1,'QA package',100,30,1); INSERT INTO package_items (package_id,service_id,included_quantity) VALUES (1,@service,1);", connection))
        {
            link.Parameters.AddWithValue("service", id); await link.ExecuteNonQueryAsync();
        }
        await Call("PATCH", path + "/status", 200, new { isActive = false }, admin);
        await Call("GET", publicPath, 404);
        await Call("GET", path, 200, token: admin);
        await Call("DELETE", path, 405, token: admin);
        await using (var count = new MySqlCommand("SELECT COUNT(*) FROM package_items WHERE service_id=@id", connection))
        {
            count.Parameters.AddWithValue("id", id);
            Check(Convert.ToInt64(await count.ExecuteScalarAsync()) == 1, "deactivation preserves package references");
        }
        Console.WriteLine("PASS: catalog lifecycle, parent visibility, composed filters and references retained after deactivation.");

        async Task<JsonElement> Call(string method, string url, int expected, object? body = null, string? token = null)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request);
            if ((int)response.StatusCode != expected) throw new InvalidOperationException($"{method} {url}: expected {expected}, got {(int)response.StatusCode}.");
            if (expected == 201) Check(response.Headers.Location?.OriginalString.StartsWith("/api/catalog/admin/", StringComparison.Ordinal) == true, "created response has canonical Location");
            if (token == admin && expected is 200 or 201) Check(response.Headers.CacheControl?.NoStore == true, "admin data is not cached");
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(text)) return default;
            using var document = JsonDocument.Parse(text); return document.RootElement.Clone();
        }
    }

    private static object ServiceBody(long categoryId, string name = "Vệ sinh sofa QA", decimal price = 150000.11m, int duration = 60, bool active = true) =>
        new { categoryId, name, description = "Mô tả QA", unit = "bộ", basePrice = price, durationMinutes = duration, isActive = active };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("FAILED: " + message); }
}
