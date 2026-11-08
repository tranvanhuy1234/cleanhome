using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

internal static class OrderWorkflowChecks
{
    public static async Task RunAsync(HttpClient http, string orderConnection, string staffConnection,
        (long Id, string Token) customer, (long Id, string Token) other, string admin, long staffUserId, string staffToken,
        Action stopStaff, Func<Task> restartStaff,int staffPort,string internalKey)
    {
        if (!Regex.IsMatch(new MySqlConnectionStringBuilder(orderConnection).Database, "^cleanhome_test_[a-f0-9]{12}_order$") ||
            !Regex.IsMatch(new MySqlConnectionStringBuilder(staffConnection).Database, "^cleanhome_test_[a-f0-9]{12}_staff$"))
            throw new InvalidOperationException("Workflow tests require isolated QA databases.");
        const string staffAdmin = "/api/staff/admin";
        const string adminOrders = "/api/orders/admin/orders";
        var fixture = Guid.NewGuid().ToString("N"); var phoneSequence = 0;
        const string password = "WorkflowTest#42";
        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(10); var end = start.AddHours(2);

        foreach (var route in new[] { staffAdmin + "/branches", staffAdmin + "/employees", staffAdmin + "/skills", adminOrders })
        {
            await Call("GET", route, 401); await Call("GET", route, 403, token: customer.Token); await Call("GET", route, 403, token: staffToken);
        }
        var branch = await Call("POST", staffAdmin + "/branches", 201, new { name = "QA branch", addressText = "123 QA street", serviceArea = new { city = "QA" }, latitude = 10.7, longitude = 106.6, status = "active" }, admin);
        var branchId = branch.GetProperty("id").GetInt64();
        var skill = await Call("POST", staffAdmin + "/skills", 201, new { name = "QA cleaning", description = "Qualified cleaning" }, admin);
        var skillId = skill.GetProperty("id").GetInt64();
        var employeeA = await Employee(staffUserId);
        var staffB = await StaffAccount("b"); var staffC = await StaffAccount("c");
        var employeeB = await Employee(staffB.Id); var employeeC = await Employee(staffC.Id);
        await Call("POST", staffAdmin + "/employees", 409, EmployeeBody(staffUserId), admin);
        await Call("POST", staffAdmin + "/employees", 409, EmployeeBody(customer.Id), admin);
        var me = await Call("GET", "/api/staff/me/profile", 200, token: staffToken);
        Check(me.GetProperty("id").GetInt64() == employeeA, "professional employee ID maps to authenticated account");
        await Call("PUT", staffAdmin + $"/employees/{employeeA}/skills", 204, new { skills = new[] { new { skillId, level = "qualified" } } }, admin);
        var availabilityIds = new Dictionary<long, long>();
        foreach (var employee in new[] { employeeA, employeeB, employeeC })
        {
            var availability = await Call("POST", staffAdmin + $"/employees/{employee}/availability", 201,
                new { startsAt = start.Date, endsAt = start.Date.AddDays(2), status = "available" }, admin);
            availabilityIds[employee] = availability.GetProperty("id").GetInt64();
        }
        await Call("POST", staffAdmin + $"/employees/{employeeA}/availability", 400,
            new { startsAt = end, endsAt = start, status = "available" }, admin);
        Console.WriteLine("PASS: branches, staff account validation, employee profiles, skills and availability setup.");

        await using var orderDb = new MySqlConnection(orderConnection); await orderDb.OpenAsync();
        await using (var seed = new MySqlCommand("""
            INSERT INTO bookings (id,customer_id,branch_id,address_snapshot,scheduled_start,scheduled_end,status,total_amount,code,subtotal_amount,discount_amount)
            VALUES (1,@owner,@branch,'{"recipient":"QA contact","phone":"0901234567","addressText":"QA destination","latitude":10.7769,"longitude":106.7009}',@start,@end,'pending',100,'WF-1',100,0),
            (2,@owner,@branch,'{}',@start,@end,'pending',100,'WF-2',100,0),
            (3,@owner,@branch,'{}',@later,@laterEnd,'pending',180,'WF-3',200,20),
            (4,@other,@branch,'{}',@start,@end,'pending',100,'WF-4',100,0),
            (5,@owner,@branch,'{}',@start,@end,'completed',100,'WF-5',100,0),
            (6,@owner,@branch,'{}',@late,@lateEnd,'pending',100,'WF-6',100,0),
            (7,@owner,@branch,'{}',@start,@end,'pending',100,'WF-7',100,0),
            (8,@owner,@branch,'{}',@start,@end,'pending',100,'WF-8',100,0);
            INSERT INTO subscriptions (id,user_id,package_id,starts_at,expires_at,purchase_price,status,package_name_snapshot)
            VALUES (1,@owner,1,'2026-01-01','2028-01-01',100,'active','QA package');
            INSERT INTO entitlements (id,subscription_id,service_id,purchased_quantity,unit_snapshot) VALUES (1,1,11,10,'hour');
            INSERT INTO booking_items (id,booking_id,service_id,entitlement_id,service_name_snapshot,quantity,unit_price,unit_snapshot)
            VALUES (1,3,11,1,'Historical QA',2,100,'hour');
            INSERT INTO entitlement_usages (entitlement_id,booking_item_id,quantity,status) VALUES (1,1,2,'reserved');
            INSERT INTO voucher_redemptions (voucher_id,booking_id,code_snapshot,eligible_amount,discount_amount,customer_id)
            VALUES (1,3,'QA10',200,20,@owner);
            """, orderDb))
        {
            seed.Parameters.AddWithValue("owner", customer.Id); seed.Parameters.AddWithValue("other", other.Id); seed.Parameters.AddWithValue("branch", branchId);
            seed.Parameters.AddWithValue("start", start); seed.Parameters.AddWithValue("end", end);
            seed.Parameters.AddWithValue("later", start.AddHours(4)); seed.Parameters.AddWithValue("laterEnd", end.AddHours(4));
            seed.Parameters.AddWithValue("late", start.AddHours(8)); seed.Parameters.AddWithValue("lateEnd", end.AddHours(8));
            await seed.ExecuteNonQueryAsync();
        }
        var adminList = await Call("GET", adminOrders, 200, token: admin);
        Check(adminList.GetProperty("total").GetInt64() == 8, "admin sees all owners' bookings");
        var filtered = await Call("GET", adminOrders + "?customerId=" + other.Id, 200, token: admin);
        Check(filtered.GetProperty("total").GetInt64() == 1, "admin can filter by customer");
        await Call("POST", "/api/orders/me/4/cancellations", 404, new { reason = "Must not cancel other owner" }, customer.Token);
        await Call("GET", "/api/orders/me/3/cancellations", 404, token: other.Token);
        await Call("POST", "/api/orders/me/5/cancellations", 409, new { reason = "Completed order" }, customer.Token);
        var cancel = await Call("POST", "/api/orders/me/3/cancellations", 201, new { reason = "Khách thay đổi lịch" }, customer.Token);
        var cancelId = cancel.GetProperty("id").GetInt64();
        await Call("POST", "/api/orders/me/3/cancellations", 409, new { reason = "Duplicate request" }, customer.Token);
        await Call("POST", adminOrders + "/3/confirm", 409, new { }, admin);
        await Call("POST", adminOrders + $"/3/cancellations/{cancelId}/reject", 200, new { note = "Please reconsider" }, admin);
        await Call("POST", adminOrders + $"/3/cancellations/{cancelId}/reject", 409, new { }, admin);
        await Call("POST", "/api/orders/me/3/cancellations", 409, new { reason = "Stale version", expectedVersion = 1 }, customer.Token);
        cancel = await Call("POST", "/api/orders/me/3/cancellations", 201, new { reason = "Hủy sau khi kiểm tra lại" }, customer.Token);
        cancelId = cancel.GetProperty("id").GetInt64();
        var approveKey = Key();
        var approved = await Operation(adminOrders + $"/3/cancellations/{cancelId}/approve", new { idempotencyKey = approveKey, note = "Approved" }, admin);
        var operationId = approved.GetProperty("id").GetString()!;
        var replay = await Operation(adminOrders + $"/3/cancellations/{cancelId}/approve", new { idempotencyKey = approveKey, note = "Approved" }, admin);
        Check(replay.GetProperty("id").GetString() == operationId, "idempotent cancellation returns original operation");
        await Call("POST", adminOrders + $"/3/cancellations/{cancelId}/approve", 409, new { idempotencyKey = approveKey, note = "Changed payload" }, admin);
        var canceledBooking = await Call("GET", "/api/orders/me/3", 200, token: customer.Token);
        Check(canceledBooking.GetProperty("status").GetString() == "cancelled", "approved cancellation updates the booking");
        Check(await Scalar(orderDb, "SELECT COUNT(*) FROM entitlement_usages WHERE status='released'") == 1 &&
            await Scalar(orderDb, "SELECT COUNT(*) FROM voucher_redemptions WHERE status='CANCELLED'") == 1 &&
            await Scalar(orderDb, "SELECT COUNT(*) FROM integration_events WHERE event_type='booking_cancelled'") == 1,
            "cancellation releases local reservations and records a durable financial integration event");
        await Call("GET", "/api/orders/operations/" + operationId, 404, token: customer.Token);
        Console.WriteLine("PASS: cancellation ownership, duplicate/stale requests, reject/approve, idempotency and reservation release.");

        await Operation(adminOrders + "/1/assignments", Assign(employeeA), admin, 409);
        await Call("POST", adminOrders + "/1/confirm", 200, new { expectedVersion = 1 }, admin);
        await Call("POST", adminOrders + "/1/confirm", 409, new { }, admin);
        var failedKey = Key();
        var pairBody = new { idempotencyKey = failedKey, employeeIds = new[] { employeeA, employeeB }, requiredSkillIds = new[] { skillId } };
        await Operation(adminOrders + "/1/assignments", pairBody, admin, 409);
        var noAssignments = await Call("GET", staffAdmin + "/bookings/1/assignments", 200, token: admin);
        Check(noAssignments.GetArrayLength() == 0, "multi-employee failure rolls back every assignment");
        await Call("PUT", staffAdmin + $"/employees/{employeeB}/skills", 204, new { skills = new[] { new { skillId, level = "qualified" } } }, admin);
        await Operation(adminOrders + "/1/assignments", pairBody, admin, 409);
        var assignKey = Key();
        var pair = new { idempotencyKey = assignKey, employeeIds = new[] { employeeA, employeeB }, requiredSkillIds = new[] { skillId } };
        var assigned = await Operation(adminOrders + "/1/assignments", pair, admin);
        var assignmentReplay = await Operation(adminOrders + "/1/assignments", pair, admin);
        Check(assigned.GetProperty("id").GetString() == assignmentReplay.GetProperty("id").GetString(), "assignment replay is exactly the original operation");
        var jobs = await Call("GET", "/api/orders/me/1/assignments", 200, token: customer.Token);
        Check(jobs.GetArrayLength() == 2, "one booking supports multiple employees");
        const string destinationRoute = "/api/orders/staff/orders/1/destination";
        await Call("GET",destinationRoute,404,token:staffToken);
        var jobA = jobs.EnumerateArray().Single(j => j.GetProperty("employeeId").GetInt64() == employeeA).GetProperty("id").GetInt64();
        var jobB = jobs.EnumerateArray().Single(j => j.GetProperty("employeeId").GetInt64() == employeeB).GetProperty("id").GetInt64();
        await Call("GET", "/api/orders/me/1/assignments", 404, token: other.Token);
        var ownJobs = await Call("GET", "/api/staff/me/jobs", 200, token: staffToken);
        Check(ownJobs.GetArrayLength() == 1 && ownJobs[0].GetProperty("id").GetInt64() == jobA, "staff job list enforces ownership");
        await Call("DELETE", staffAdmin + $"/employees/{employeeA}/availability/{availabilityIds[employeeA]}", 409, token: admin);
        await Call("PUT", staffAdmin + $"/employees/{employeeA}/skills", 409, new { skills = Array.Empty<object>() }, admin);
        await Call("PUT", staffAdmin + $"/employees/{employeeA}", 409, new { branchId, verificationStatus = "verified", experienceYears = 2, status = "inactive" }, admin);
        Console.WriteLine("PASS: verified/qualified staff, atomic many-employee assignment and calendar/profile integrity.");

        await Operation("/api/orders/staff/orders/1/progress", Progress(jobA, "done"), staffToken, 409);
        await Call("POST", "/api/orders/staff/orders/1/progress", 404, Progress(jobA, "accepted"), staffB.Token);
        await Operation("/api/orders/staff/orders/1/progress", Progress(jobA, "accepted"), staffToken);
        await Call("GET", destinationRoute, 401);
        await Call("GET", destinationRoute, 403, token: customer.Token);
        await Call("GET", destinationRoute, 404, token: staffC.Token);
        await Call("GET", "/api/orders/staff/orders/999999/destination", 404, token: staffToken);
        var destination = await Call("GET", destinationRoute, 200, token: staffToken);
        Check(destination.GetProperty("latitude").GetDecimal() == 10.7769m && destination.GetProperty("addressText").GetString() == "QA destination", "staff receives original destination snapshot");
        Check(!destination.TryGetProperty("customerId", out _) && !destination.TryGetProperty("totalAmount", out _), "destination response is limited to navigation/contact");
        await using (var legacy = new MySqlCommand("UPDATE bookings SET address_snapshot='{\"address\":\"Legacy QA address\",\"latitude\":999,\"longitude\":106}' WHERE id=1", orderDb)) await legacy.ExecuteNonQueryAsync();
        destination = await Call("GET", destinationRoute, 200, token: staffToken);
        Check(destination.GetProperty("latitude").ValueKind == JsonValueKind.Null && destination.GetProperty("longitude").ValueKind == JsonValueKind.Null && destination.GetProperty("addressText").GetString() == "Legacy QA address", "invalid pin uses text fallback without inventing coordinates");
        Console.WriteLine("PASS: navigation destination is scoped to active assigned staff; legacy address and invalid coordinate fallback.");

        cancel = await Call("POST", "/api/orders/me/1/cancellations", 201, new { reason = "Please pause before starting" }, customer.Token);
        await Call("POST", "/api/orders/staff/orders/1/progress", 409, Progress(jobA, "in_progress"), staffToken);
        await Call("POST", adminOrders + $"/1/cancellations/{cancel.GetProperty("id").GetInt64()}/reject", 200, new { note = "Continue work" }, admin);
        var startKey = Key(); var startBody = new { idempotencyKey = startKey, assignmentId = jobA, nextStatus = "in_progress" };
        await Operation("/api/orders/staff/orders/1/progress", startBody, staffToken);
        var duringWork=await Call("POST", "/api/orders/me/1/cancellations", 201, new { reason = "Request admin review during work" }, customer.Token);
        await Call("POST", adminOrders+$"/1/cancellations/{duringWork.GetProperty("id").GetInt64()}/reject",200,new{note="Continue after reviewing in-progress request"},admin);
        await Operation("/api/orders/staff/orders/1/progress", Progress(jobB, "accepted"), staffB.Token);
        await Operation("/api/orders/staff/orders/1/progress", Progress(jobB, "in_progress"), staffB.Token);
        await Operation("/api/orders/staff/orders/1/progress", Progress(jobA, "done"), staffToken);
        var halfDone = await Call("GET", "/api/orders/me/1", 200, token: customer.Token);
        Check(halfDone.GetProperty("status").GetString() == "in_progress", "first employee completion does not finish a multi-person booking");
        await Operation("/api/orders/staff/orders/1/progress", Progress(jobB, "done"), staffB.Token);
        var allDone = await Call("GET", "/api/orders/me/1", 200, token: customer.Token);
        Check(allDone.GetProperty("status").GetString() == "completed", "all assigned employees must finish before booking completes");
        var statsDate=DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd");
        var statsRoute=$"/api/orders/staff/me/statistics?from={statsDate}&to={statsDate}";
        await Call("GET",statsRoute,401);
        await Call("GET",statsRoute,403,token:customer.Token);
        await Call("GET",statsRoute,403,token:admin);
        var statsA=await Call("GET",statsRoute,200,token:staffToken);
        var statsB=await Call("GET",statsRoute,200,token:staffB.Token);
        var statsC=await Call("GET",statsRoute,200,token:staffC.Token);
        Check(statsA.GetProperty("summary").GetProperty("allocatedRevenue").GetDecimal()==50m&&statsB.GetProperty("summary").GetProperty("allocatedRevenue").GetDecimal()==50m,"completed order split equally across accepted staff");
        Check(statsC.GetProperty("summary").GetProperty("allocatedRevenue").GetDecimal()==0m,"other staff has no revenue from foreign work");
        var forged=await Call("GET",statsRoute+"&userId="+staffC.Id,200,token:staffToken);
        Check(forged.GetProperty("summary").GetProperty("allocatedRevenue").GetDecimal()==50m,"query cannot override authenticated staff identity");
        Check(statsA.GetProperty("history").GetProperty("total").GetInt32()==1&&statsA.GetProperty("revenue")[0].GetProperty("revenue").GetDecimal()==50m,"history and chart agree with summary");
        await Call("GET","/api/orders/staff/me/statistics?from=2026-12-31&to=2026-01-01",400,token:staffToken);
        Console.WriteLine("PASS: personal statistics split revenue, completion-time range, chart/history consistency and staff privacy.");
        await Call("GET", destinationRoute, 404, token: staffToken);
        await Operation("/api/orders/staff/orders/1/progress", startBody, staffToken);
        allDone = await Call("GET", "/api/orders/me/1", 200, token: customer.Token);
        Check(allDone.GetProperty("status").GetString() == "completed", "replaying an older successful step cannot roll status backwards");
        Console.WriteLine("PASS: employee ownership, strict progression, cancellation hold and aggregate multi-person completion.");

        foreach (var id in new[] { 2, 8 }) await Call("POST", adminOrders + $"/{id}/confirm", 200, new { }, admin);
        var concurrent = await Task.WhenAll(AnyOperation(adminOrders + "/2/assignments", Assign(employeeC), admin), AnyOperation(adminOrders + "/8/assignments", Assign(employeeC), admin));
        Check(concurrent.Count(r => r.GetProperty("state").GetString() == "succeeded") == 1 && concurrent.Count(r => r.GetProperty("state").GetString() == "failed") == 1,
            "concurrent overlapping bookings cannot claim the same employee");
        Console.WriteLine("PASS: concurrent booking assignment is serialized by employee and prevents overlap.");

        await Call("POST", adminOrders + "/6/confirm", 200, new { }, admin);
        stopStaff();
        var pendingReply = await Send("POST", adminOrders + "/6/assignments", Assign(employeeA), admin);
        Check(pendingReply.Status == 202 && pendingReply.Body.GetProperty("state").GetString() == "pending", "downstream outage reports accepted/pending, not success");
        await Call("POST", "/api/orders/me/6/cancellations", 409, new { reason = "Operation still pending" }, customer.Token);
        await restartStaff();
        var recovered = await Await(pendingReply.Body);
        Check(recovered.GetProperty("state").GetString() == "succeeded", "durable operation recovers after staff restart");
        Console.WriteLine("PASS: durable workflow retry across staff outage; pending operation blocks conflicting changes.");
        await DispatchChecks.RunAsync(http,orderConnection,staffConnection,staffPort,internalKey,branchId,admin,customer.Token,[(staffUserId,staffToken),(staffB.Id,staffB.Token),(staffC.Id,staffC.Token)]);

        async Task<long> Employee(long userId)
        { var r = await Call("POST", staffAdmin + "/employees", 201, EmployeeBody(userId), admin); return r.GetProperty("id").GetInt64(); }
        object EmployeeBody(long userId) => new { userId, branchId, verificationStatus = "verified", experienceYears = 2, status = "active" };
        async Task<(long Id, string Token)> StaffAccount(string suffix)
        {
            var email = $"wf-{suffix}-{fixture}@example.invalid";
            var account = await Call("POST", "/api/auth/accounts", 201, new { email, password, fullName = "Workflow staff " + suffix, phone = "08" + (++phoneSequence).ToString("D8"), role = "staff" }, admin);
            var login = await Call("POST", "/api/auth/login", 200, new { email, password });
            return (account.GetProperty("id").GetInt64(), login.GetProperty("accessToken").GetString()!);
        }
        object Assign(long employee) => new { idempotencyKey = Key(), employeeIds = new[] { employee }, requiredSkillIds = Array.Empty<long>() };
        object Progress(long assignmentId, string nextStatus) => new { idempotencyKey = Key(), assignmentId, nextStatus };
        async Task<JsonElement> Operation(string route, object body, string token, int expected = 200)
        {
            var result = await AnyOperation(route, body, token);
            Check(expected == 200 ? result.GetProperty("state").GetString() == "succeeded" :
                result.GetProperty("state").GetString() == "failed" && result.GetProperty("errorStatus").GetInt32() == expected, "workflow result matches expected business outcome");
            return result;
        }
        async Task<JsonElement> AnyOperation(string route, object body, string token)
        {
            var reply = await Send("POST", route, body, token);
            if (reply.Status is 400 or 404 or 409) return JsonSerializer.SerializeToElement(new { state = "failed", errorStatus = reply.Status });
            if (reply.Status is not (200 or 202)) throw new InvalidOperationException($"POST {route}: unexpected {reply.Status}.");
            return await Await(reply.Body);
        }
        async Task<JsonElement> Await(JsonElement op)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (op.GetProperty("state").GetString() == "pending" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(250); op = await Call("GET", "/api/orders/operations/" + op.GetProperty("id").GetString(), 200, token: admin);
            }
            Check(op.GetProperty("state").GetString() != "pending", "workflow finished before timeout"); return op;
        }
        async Task<JsonElement> Call(string method, string route, int status, object? body = null, string? token = null)
        {
            var reply = await Send(method, route, body, token);
            if (reply.Status != status) throw new InvalidOperationException($"{method} {route}: expected {status}, got {reply.Status}.");
            return reply.Body;
        }
        async Task<(int Status, JsonElement Body)> Send(string method, string route, object? body = null, string? token = null)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), route);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request); var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(text)) return ((int)response.StatusCode, default);
            using var json = JsonDocument.Parse(text); return ((int)response.StatusCode, json.RootElement.Clone());
        }
    }
    private static string Key() => Guid.NewGuid().ToString("N");
    private static async Task<long> Scalar(MySqlConnection connection, string sql)
    { await using var q = new MySqlCommand(sql, connection); return Convert.ToInt64(await q.ExecuteScalarAsync()); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("FAILED: " + message); }
}
