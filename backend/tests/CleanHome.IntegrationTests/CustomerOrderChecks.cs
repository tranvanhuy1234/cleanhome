using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

internal static class CustomerOrderChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, (long Id, string Token) customer,
        (long Id, string Token) other, string admin, string staff)
    {
        if (!Regex.IsMatch(new MySqlConnectionStringBuilder(connectionString).Database, "^cleanhome_test_[a-f0-9]{12}_order$"))
            throw new InvalidOperationException("Order fixtures require isolated QA database.");
        foreach (var path in new[] { "/api/orders/me", "/api/orders/me/history", "/api/orders/me/1" })
        {
            await Get(path, 401);
            await Get(path, 403, admin);
            await Get(path, 403, staff);
        }
        var empty = await Get("/api/orders/me", 200, customer.Token);
        Check(empty.GetProperty("total").GetInt64() == 0 && empty.GetProperty("items").GetArrayLength() == 0, "new customer has empty orders");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var seed = new MySqlCommand("""
            INSERT INTO bookings (id,customer_id,branch_id,address_snapshot,scheduled_start,scheduled_end,status,total_amount,code,subtotal_amount,discount_amount,created_at)
            VALUES
            (1,@customer,1,'{"recipient":"Khách hàng A","address":"Địa chỉ snapshot"}','2026-01-01','2026-01-01 02:00:00','completed',190.50,'QA-001',200.50,10,'2025-12-01'),
            (2,@customer,1,'{}','2026-02-01','2026-02-01 02:00:00','completed',3000,'QA-002',3000,0,'2025-12-02'),
            (3,@customer,1,'{}','2026-03-01','2026-03-01 02:00:00','pending',100,'QA-003',100,0,'2025-12-03'),
            (4,@other,1,'{"address":"DO-NOT-LEAK"}','2026-04-01','2026-04-01 02:00:00','completed',100,'OTHER-004',100,0,'2025-12-04'),
            (5,@customer,1,'{}','2026-04-01','2026-04-01 02:00:00','cancelled',100,'QA-005',100,0,'2025-12-05'),
            (6,@customer,1,'{}','2026-05-01','2026-05-01 02:00:00','in_progress',100,'QA-006',100,0,'2025-12-06'),
            (7,@customer,1,'{}','2026-06-01','2026-06-01 02:00:00','confirmed',100,'QA-007',100,0,'2025-12-07');
            INSERT INTO booking_items (booking_id,service_id,service_name_snapshot,quantity,unit_price,unit_snapshot) VALUES
            (1,11,'Sofa_%!',1,200.50,'bộ'),(1,33,'Giặt rèm',1,0,'bộ'),(2,22,'Dọn nhà',2,1500,'giờ'),
            (3,11,'Sofa_%!',1,100,'bộ'),(4,22,'SecretOtherService',1,100,'bộ'),(5,11,'Sofa_%!',1,100,'bộ');
            INSERT INTO booking_status_history (id,booking_id,changed_by,status,note,created_at) VALUES
            (3,1,@customer,'completed','Hoàn thành','2026-01-01 02:00:00'),
            (1,1,@customer,'pending','Tạo đơn','2025-12-01'),(2,1,@customer,'confirmed','Xác nhận','2025-12-02'),
            (4,4,@other,'completed','DO-NOT-LEAK','2026-04-01');
            """, connection))
        {
            seed.Parameters.AddWithValue("customer", customer.Id); seed.Parameters.AddWithValue("other", other.Id);
            await seed.ExecuteNonQueryAsync();
        }

        var page = await Get("/api/orders/me", 200, customer.Token);
        Check(page.GetProperty("total").GetInt64() == 6 && Ids(page).SequenceEqual(new long[] { 7, 6, 5, 3, 2, 1 }), "list contains only own orders in stable scheduled order");
        var blankStatus = await Get("/api/orders/me?status=", 200, customer.Token);
        Check(Ids(blankStatus).SequenceEqual(Ids(page)), "blank optional status preserves the full own-order list");
        var otherPage = await Get("/api/orders/me", 200, other.Token);
        Check(otherPage.GetProperty("total").GetInt64() == 1 && Ids(otherPage).Single() == 4, "other customer sees only own order");
        var spoof = await Get("/api/orders/me?customerId=" + other.Id, 200, customer.Token);
        Check(Ids(spoof).SequenceEqual(Ids(page)), "query cannot replace authenticated owner");
        var detail = await Get("/api/orders/me/1", 200, customer.Token);
        Check(detail.GetProperty("customerId").GetInt64() == customer.Id && detail.GetProperty("totalAmount").GetDecimal() == 190.50m &&
            detail.GetProperty("subtotalAmount").GetDecimal() == 200.50m && detail.GetProperty("discountAmount").GetDecimal() == 10m &&
            detail.GetProperty("addressSnapshot").GetProperty("recipient").GetString() == "Khách hàng A", "detail preserves amounts and original address snapshot");
        Check(detail.GetProperty("items").GetArrayLength() == 2 && detail.GetProperty("items")[0].GetProperty("serviceName").GetString() == "Sofa_%!" &&
            detail.GetProperty("items")[0].GetProperty("entitlementId").ValueKind == JsonValueKind.Null, "detail uses historical service snapshots without catalog dependencies");
        Check(detail.GetProperty("history").EnumerateArray().Select(row => row.GetProperty("id").GetInt64()).SequenceEqual(new long[] { 1, 2, 3 }), "history sorted chronologically");
        var notOwned = await Get("/api/orders/me/4", 404, customer.Token);
        var missing = await Get("/api/orders/me/" + long.MaxValue, 404, customer.Token);
        Check(notOwned.GetProperty("title").GetString() == missing.GetProperty("title").GetString(), "missing and foreign orders return identical public errors");
        await Get("/api/orders/me/1", 404, other.Token);
        await Get("/api/orders/me/4", 200, other.Token);
        await Get("/api/orders/me/0", 400, customer.Token);
        await Get("/api/orders/me/-1", 400, customer.Token);
        await Get("/api/orders/me/9223372036854775808", 404, customer.Token);
        Console.WriteLine("PASS: customer orders: authorization, ownership, snapshots, exact amounts and chronological history.");

        var history = await Get("/api/orders/me/history", 200, customer.Token);
        Check(history.GetProperty("total").GetInt64() == 2 && Ids(history).SequenceEqual(new long[] { 2, 1 }), "history contains completed orders only");
        var overridden = await Get("/api/orders/me/history?status=pending", 200, customer.Token);
        Check(Ids(overridden).SequenceEqual(Ids(history)), "history cannot include pending orders through status query");
        var dates = "?from=" + Uri.EscapeDataString("2026-01-01T07:00:00+07:00") + "&to=" + Uri.EscapeDataString("2026-02-01T07:00:00+07:00");
        var filtered = await Get("/api/orders/me/history" + dates, 200, customer.Token);
        Check(filtered.GetProperty("total").GetInt64() == 1 && Ids(filtered).Single() == 1, "UTC-normalized range includes from and excludes to");
        filtered = await Get("/api/orders/me/history?serviceId=22", 200, customer.Token);
        Check(Ids(filtered).Single() == 2, "history filters by service ID");
        filtered = await Get("/api/orders/me/history?serviceId=11&search=" + Uri.EscapeDataString("soFA_%!"), 200, customer.Token);
        Check(Ids(filtered).Single() == 1, "history filters combine case-insensitive snapshot search and service ID");
        foreach (var query in new[] { "search=SecretOtherService", "search=" + Uri.EscapeDataString("' OR 1=1 --"), "serviceId=999999" })
        {
            filtered = await Get("/api/orders/me/history?" + query, 200, customer.Token);
            Check(filtered.GetProperty("total").GetInt64() == 0 && Ids(filtered).Length == 0, "history search cannot expose foreign orders or SQL data");
        }
        filtered = await Get("/api/orders/me?status=pending", 200, customer.Token);
        Check(Ids(filtered).Single() == 3, "order list supports status filter");
        filtered = await Get("/api/orders/me/history?page=2&pageSize=1", 200, customer.Token);
        Check(filtered.GetProperty("total").GetInt64() == 2 && Ids(filtered).Single() == 1, "history pagination preserves filtered total");
        foreach (var query in new[] { "page=0", "pageSize=101", "serviceId=0", "from=invalid", "from=2026-02-01&to=2026-01-01", "search=" + new string('x', 151) })
            await Get("/api/orders/me/history?" + query, 400, customer.Token);
        await Get("/api/orders/me?status=invalid", 400, customer.Token);
        var after = await Get("/api/orders/me/1", 200, customer.Token);
        Check(after.GetRawText() == detail.GetRawText(), "read endpoints do not mutate booking data");
        Console.WriteLine("PASS: service history: time range, service/name filters, pagination, validation and immutable reads.");

        async Task<JsonElement> Get(string route, int status, string? token = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request);
            if ((int)response.StatusCode != status) throw new InvalidOperationException($"GET {route}: expected {status}, got {(int)response.StatusCode}.");
            if (status == 200 && token is not null) Check(response.Headers.CacheControl?.NoStore == true, "private order response cannot be cached");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone();
        }
    }

    private static long[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64()).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("FAILED: " + message); }
}
