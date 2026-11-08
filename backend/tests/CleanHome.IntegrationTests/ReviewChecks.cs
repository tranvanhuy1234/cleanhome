using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

internal static class ReviewChecks
{
    public static async Task RunAsync(HttpClient http, string customer, string other, string admin, string staff)
    {
        var assignments = await Call("GET", "/api/orders/me/1/assignments", 200, token: customer);
        var employeeA = assignments[0].GetProperty("employeeId").GetInt64();
        var employeeB = assignments[1].GetProperty("employeeId").GetInt64();
        object Body(long booking, long employee, int rating = 5) => new { bookingId = booking, employeeId = employee, rating, comment = "  Nhân viên thực hiện tốt  " };
        await Call("POST", "/api/reviews/me", 401, Body(1, employeeA));
        await Call("POST", "/api/reviews/me", 403, Body(1, employeeA), staff);
        await Call("POST", "/api/reviews/me", 403, Body(1, employeeA), admin);
        await Call("GET", "/api/reviews/admin/reviews", 403, token: customer);
        await Call("POST", "/api/reviews/me", 400, Body(1, employeeA, 6), customer);
        await Call("POST", "/api/reviews/me", 400, new { bookingId = 1, employeeId = employeeA, rating = 5, comment = " " }, customer);
        await Call("POST", "/api/reviews/me", 400, new { bookingId = 1, employeeId = employeeA, rating = 5, comment = "QA", customerId = 999 }, customer);
        await Call("POST", "/api/reviews/me", 404, Body(1, employeeA), other);
        await Call("POST", "/api/reviews/me", 409, Body(2, employeeA), customer);
        await Call("POST", "/api/reviews/me", 409, Body(5, employeeA), customer);
        await Call("POST", "/api/reviews/me", 409, Body(1, long.MaxValue), customer);
        var a = await Call("POST", "/api/reviews/me", 201, Body(1, employeeA), customer);
        var aId = a.GetProperty("id").GetInt64();
        Check(a.GetProperty("comment").GetString() == "Nhân viên thực hiện tốt" && !a.GetProperty("isHidden").GetBoolean(), "review is trimmed and visible initially");
        await Call("POST", "/api/reviews/me", 409, Body(1, employeeA), customer);
        await Call("POST", "/api/reviews/me", 201, Body(1, employeeB, 3), customer);
        await Call("GET", "/api/reviews/me/" + aId, 404, token: other);
        await Call("GET", "/api/reviews/me/" + aId, 200, token: customer);
        Console.WriteLine("PASS: reviews require owner, completed booking and completed employee assignment; one review per booking/employee.");

        var laterJobs = await Call("GET", "/api/orders/me/6/assignments", 200, token: customer);
        var later = laterJobs[0];
        // Order workflow fixture assigns the original staff to booking 6.
        foreach (var nextStatus in new[] { "accepted", "in_progress", "done" })
        {
            var op = await CallAny("POST", "/api/orders/staff/orders/6/progress",
                new { idempotencyKey = Guid.NewGuid().ToString("N"), assignmentId = later.GetProperty("id").GetInt64(), nextStatus }, staff);
            Check(op.Status is 200 or 202, "staff can progress second booking");
            var state = op.Body; var deadline = DateTime.UtcNow.AddSeconds(20);
            while (state.GetProperty("state").GetString() == "pending" && DateTime.UtcNow < deadline)
            { await Task.Delay(250); state = await Call("GET", "/api/orders/operations/" + state.GetProperty("id").GetString(), 200, token: admin); }
            Check(state.GetProperty("state").GetString() == "succeeded", "second booking progresses successfully");
        }
        var laterEmployee = later.GetProperty("employeeId").GetInt64();
        Check(laterEmployee == employeeA, "rating average fixture uses same employee across two bookings");
        await Call("POST", "/api/reviews/me", 201, Body(6, employeeA, 1), customer);
        var publicPage = await Call("GET", "/api/reviews/employees/" + employeeA + "?pageSize=1", 200);
        Check(publicPage.GetProperty("total").GetInt64() == 2 && publicPage.GetProperty("averageRating").GetDecimal() == 3 && publicPage.GetProperty("items").GetArrayLength() == 1,
            "public average is over all matching reviews, not the current page");
        Check(publicPage.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "comment", "createdAt", "employeeId", "id", "rating" }), "public DTO excludes customer, booking and moderation data");
        await Call("PATCH", "/api/reviews/admin/reviews/" + aId + "/visibility", 400, new { isHidden = true }, admin);
        var hidden = await Call("PATCH", "/api/reviews/admin/reviews/" + aId + "/visibility", 200, new { isHidden = true, reason = "Moderation QA" }, admin);
        Check(hidden.GetProperty("isHidden").GetBoolean() && hidden.GetProperty("moderatedBy").GetInt64() > 0, "moderation records actor and reason");
        publicPage = await Call("GET", "/api/reviews/employees/" + employeeA, 200);
        Check(publicPage.GetProperty("total").GetInt64() == 1 && publicPage.GetProperty("averageRating").GetDecimal() == 1, "hidden review is excluded from public list and average");
        var adminPage = await Call("GET", "/api/reviews/admin/reviews?isHidden=true&rating=5&employeeId=" + employeeA, 200, token: admin);
        Check(adminPage.GetProperty("total").GetInt64() == 1, "admin filters hidden reviews and ratings");
        var mine = await Call("GET", "/api/reviews/me", 200, token: customer);
        Check(mine.GetProperty("total").GetInt64() == 3, "owner retains access to own moderated review");
        await Call("PATCH", "/api/reviews/admin/reviews/" + aId + "/visibility", 200, new { isHidden = false }, admin);
        publicPage = await Call("GET", "/api/reviews/employees/" + employeeA, 200);
        Check(publicPage.GetProperty("total").GetInt64() == 2 && publicPage.GetProperty("averageRating").GetDecimal() == 3, "restored review returns to public average");
        await Call("GET", "/api/reviews/admin/reviews?rating=6", 400, token: admin);
        await Call("GET", "/api/reviews/admin/reviews?page=0", 400, token: admin);
        await Call("GET", "/api/reviews/admin/reviews/" + long.MaxValue, 404, token: admin);
        var noReviews = await Call("GET", "/api/reviews/employees/" + long.MaxValue, 200);
        Check(noReviews.GetProperty("total").GetInt64() == 0 && noReviews.GetProperty("averageRating").GetDecimal() == 0, "employee without public reviews yields empty zero average");
        Console.WriteLine("PASS: review pagination, private/public DTOs, moderation, filters and visible-only rating aggregation.");

        async Task<JsonElement> Call(string method, string route, int expected, object? body = null, string? token = null)
        {
            var reply = await CallAny(method, route, body, token);
            if (reply.Status != expected) throw new InvalidOperationException($"{method} {route}: expected {expected}, got {reply.Status}.");
            return reply.Body;
        }
        async Task<(int Status, JsonElement Body)> CallAny(string method, string route, object? body = null, string? token = null)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), route);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request); var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(text)) return ((int)response.StatusCode, default);
            using var document = JsonDocument.Parse(text); return ((int)response.StatusCode, document.RootElement.Clone());
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("FAILED: " + message); }
}
