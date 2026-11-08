using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CleanHome.ServiceDefaults;
using MySqlConnector;

// Không dùng database ứng dụng; chỉ tạo và dọn database QA có tên được kiểm tra.
var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "backend", "CleanHome.slnx"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("Không tìm thấy repository.");
var backend = Path.Combine(root.FullName, "backend");
if(args.Contains("--staff-statistics")){await StaffStatisticsChecks.RunAsync();return;}
var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
var configured = Environment.GetEnvironmentVariable("CLEANHOME_TEST_ADMIN_CONNECTION");
if (string.IsNullOrWhiteSpace(configured))
{
    using var local = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(backend, "auth-service", "src", "appsettings.Local.json")));
    configured = local.RootElement.GetProperty("ConnectionStrings").GetProperty("Database").GetString();
}
if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("Chưa có kết nối MariaDB dành cho kiểm thử.");
var adminSettings = new MySqlConnectionStringBuilder(configured) { Database = "", DateTimeKind = MySqlDateTimeKind.Utc };
if(args.Contains("--payments")){await PaymentChecks.RunAsync(root.FullName,adminSettings.ConnectionString);return;}
if(args.Contains("--booking-creation")) { await BookingCreationChecks.RunAsync(root.FullName,adminSettings.ConnectionString);return; }
var runId = Guid.NewGuid().ToString("N")[..12];
var catalogDetailOnly = args.Contains("--catalog-detail-only", StringComparer.Ordinal);
var catalogManagement = args.Contains("--catalog-management", StringComparer.Ordinal);
var customerOrders = args.Contains("--customer-orders", StringComparer.Ordinal);
var reviewWorkflows = args.Contains("--review-workflows", StringComparer.Ordinal);
var orderWorkflows = reviewWorkflows || args.Contains("--order-workflows", StringComparer.Ordinal);
var catalogOnly = catalogDetailOnly || args.Contains("--catalog-list-only", StringComparer.Ordinal);
var databases = catalogOnly ? new[] { $"cleanhome_test_{runId}_service" }
    : reviewWorkflows ? new[] { $"cleanhome_test_{runId}_auth", $"cleanhome_test_{runId}_user", $"cleanhome_test_{runId}_order", $"cleanhome_test_{runId}_staff", $"cleanhome_test_{runId}_review" }
    : orderWorkflows ? new[] { $"cleanhome_test_{runId}_auth", $"cleanhome_test_{runId}_user", $"cleanhome_test_{runId}_order", $"cleanhome_test_{runId}_staff" }
    : catalogManagement ? new[] { $"cleanhome_test_{runId}_auth", $"cleanhome_test_{runId}_user", $"cleanhome_test_{runId}_service" }
    : customerOrders ? new[] { $"cleanhome_test_{runId}_auth", $"cleanhome_test_{runId}_user", $"cleanhome_test_{runId}_order" }
    : new[] { $"cleanhome_test_{runId}_auth", $"cleanhome_test_{runId}_user" };
var processes = new List<Process>();
var logDirectory = Path.Combine(Path.GetTempPath(), "cleanhome-api-test-" + runId);
Directory.CreateDirectory(logDirectory);
var internalKey = SessionTokens.Create();
var authPort = FreePort(); var userPort = FreePort(); var gatewayPort = FreePort();
while (userPort == authPort) userPort = FreePort();
while (gatewayPort == authPort || gatewayPort == userPort) gatewayPort = FreePort();
var catalogPort = catalogOnly ? authPort : FreePort();
while (!catalogOnly && (catalogPort == authPort || catalogPort == userPort || catalogPort == gatewayPort)) catalogPort = FreePort();
var staffPort = FreePort();
while (new[] { authPort, userPort, gatewayPort, catalogPort }.Contains(staffPort)) staffPort = FreePort();
var reviewPort = FreePort();
while (new[] { authPort, userPort, gatewayPort, catalogPort, staffPort }.Contains(reviewPort)) reviewPort = FreePort();
using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{gatewayPort}"), Timeout = TimeSpan.FromSeconds(15) };
var phoneCounter = 0;
string Phone() => "09" + RandomNumberGenerator.GetInt32(100000, 999999) + (++phoneCounter).ToString("D3");
const string password = "TestOnly#Password42";
const string changedPassword = "TestOnly#Changed43";
string? authConnection = null, userConnection = null;

try
{
    await using var admin = new MySqlConnection(adminSettings.ConnectionString);
    await admin.OpenAsync();
    for (var i = 0; i < databases.Length; i++)
    {
        var owner = catalogOnly ? "service" : i == 4 ? "review" : i == 3 ? "staff" : i == 2 ? (customerOrders || orderWorkflows ? "order" : "service") : i == 0 ? "auth" : "user";
        var source = await File.ReadAllTextAsync(Path.Combine(root.FullName, "databases", owner + "-db", "001_initial.sql"));
        await using var import = new MySqlCommand(source.Replace("`cleanhome_" + owner + "`", "`" + databases[i] + "`"), admin);
        await import.ExecuteNonQueryAsync();
        if(owner is "order" or "staff"){var lifecycle=await File.ReadAllTextAsync(Path.Combine(root.FullName,"databases",owner+"-db",owner=="order"?"006_order_lifecycle.sql":"005_order_lifecycle.sql"));await using var lifecycleCommand=new MySqlCommand(lifecycle.Replace("`cleanhome_"+owner+"`","`"+databases[i]+"`"),admin);await lifecycleCommand.ExecuteNonQueryAsync();}
        if (owner is "order" or "staff")
        {
            var migration = await File.ReadAllTextAsync(Path.Combine(root.FullName, "databases", owner + "-db", "002_workflows.sql"));
            await using var migrate = new MySqlCommand(migration.Replace("`cleanhome_" + owner + "`", "`" + databases[i] + "`"), admin);
            await migrate.ExecuteNonQueryAsync();
        }
        if(owner=="staff")
        {
            var receiptMigration=await File.ReadAllTextAsync(Path.Combine(root.FullName,"databases/staff-db/003_assignment_receipts.sql"));
            await using var receiptCommand=new MySqlCommand(receiptMigration.Replace("`cleanhome_staff`","`"+databases[i]+"`"),admin);
            await receiptCommand.ExecuteNonQueryAsync();
        }
        if(owner is "order" or "staff")
        {
            var dispatchMigration=await File.ReadAllTextAsync(Path.Combine(root.FullName,"databases",owner+"-db","004_dispatch.sql"));
            await using var dispatchCommand=new MySqlCommand(dispatchMigration.Replace("`cleanhome_"+owner+"`","`"+databases[i]+"`"),admin);
            await dispatchCommand.ExecuteNonQueryAsync();
            if(owner=="order"){var paymentSql=await File.ReadAllTextAsync(Path.Combine(root.FullName,"databases","order-db","005_payments.sql"));await using var paymentMigration=new MySqlCommand(paymentSql.Replace("`cleanhome_order`","`"+databases[i]+"`"),admin);await paymentMigration.ExecuteNonQueryAsync();}
        }
    }
    if (catalogOnly)
    {
        var catalogConnection = new MySqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databases[0] }.ConnectionString;
        await Start("service-service", "CleanHome.Catalog.Api", authPort, catalogConnection);
        await Start("api-gateway", "CleanHome.Gateway.Api", gatewayPort, null);
        await CatalogChecks.RunAsync(http, catalogConnection, catalogDetailOnly);
        return;
    }
    authConnection = new MySqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databases[0] }.ConnectionString;
    userConnection = new MySqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databases[1] }.ConnectionString;
    var userProcess = await Start("user-service", "CleanHome.User.Api", userPort, userConnection);
    var authProcess = await Start("auth-service", "CleanHome.Auth.Api", authPort, authConnection);
    string? managementConnection = null;
    if (catalogManagement)
    {
        managementConnection = new MySqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databases[2] }.ConnectionString;
        await Start("service-service", "CleanHome.Catalog.Api", catalogPort, managementConnection);
    }
    if (customerOrders || orderWorkflows)
    {
        managementConnection = new MySqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databases[2] }.ConnectionString;
        await Start("order-service", "CleanHome.Order.Api", catalogPort, managementConnection);
    }
    Process? staffProcess = null;
    string? staffConnection = null;
    if (orderWorkflows)
    {
        staffConnection = new MySqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databases[3] }.ConnectionString;
        staffProcess = await Start("staff-service", "CleanHome.Staff.Api", staffPort, staffConnection);
    }
    if (reviewWorkflows)
    {
        var reviewConnection = new MySqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databases[4] }.ConnectionString;
        await Start("review-service", "CleanHome.Review.Api", reviewPort, reviewConnection);
    }
    await Start("api-gateway", "CleanHome.Gateway.Api", gatewayPort, null);
    Pass("isolated QA databases and three services started");

    await Call("GET", "/api/auth/health/ready", 200);
    await Call("GET", "/api/users/health/ready", 200);
    await Call("GET", "/api/users/me", 401);
    await Call("POST", "/api/auth/register", 400, new { email = "invalid", password = "short", fullName = " ", phone = "abc" });
    var email = $"customer-{runId}@example.invalid";
    var phone = Phone();
    var created = await Call("POST", "/api/auth/register", 201, new { email, password, fullName = "Khách hàng kiểm thử", phone, role = "admin" });
    var id = created.GetProperty("id").GetInt64();
    var token = created.GetProperty("accessToken").GetString()!;
    Check(created.GetProperty("role").GetString() == "customer", "public registration cannot escalate role");
    Check(created.GetProperty("profileReady").GetBoolean(), "profile provisioned through internal API");
    var profile = await Call("GET", "/api/users/me", 200, token: token);
    Check(profile.GetProperty("fullName").GetString() == "Khách hàng kiểm thử", "Vietnamese profile round trip");
    await Call("GET", "/api/auth/accounts", 403, token: token);
    await Call("POST", "/api/auth/register", 409, new { email, password, fullName = "Duplicate", phone = Phone() });
    await using (var db = new MySqlConnection(authConnection))
    {
        await db.OpenAsync();
        await using var hash = db.Sql("SELECT password_hash FROM accounts WHERE id=@id", ("id", id));
        var stored = Convert.ToString(await hash.ExecuteScalarAsync());
        Check(!string.IsNullOrEmpty(stored) && stored != password, "password stored as hash");
        await using var session = db.Sql("SELECT COUNT(*) FROM auth_sessions WHERE account_id=@id AND token_hash=@hash", ("id", id), ("hash", SessionTokens.Hash(token)));
        Check(Convert.ToInt64(await session.ExecuteScalarAsync()) == 1, "session stored as token hash");
    }
    Pass("registration, validation, duplicate email and customer permissions");

    for (var i = 0; i < 5; i++) await Call("POST", "/api/auth/login", 401, new { email, password = "Incorrect#42" });
    await Call("POST", "/api/auth/login", 401, new { email, password });
    await AuthSql("UPDATE accounts SET locked_until=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 MINUTE) WHERE id=@id", id);
    var login = await Call("POST", "/api/auth/login", 200, new { email = email.ToUpperInvariant(), password });
    token = login.GetProperty("accessToken").GetString()!;
    Pass("persistent lockout, expiry and case-insensitive email login");

    var adminEmail = $"admin-{runId}@example.invalid";
    var adminPhone = Phone();
    var adminAccount = await Call("POST", "/api/auth/register", 201, new { email = adminEmail, password, fullName = "QA administrator", phone = adminPhone });
    var adminId = adminAccount.GetProperty("id").GetInt64();
    var adminToken = adminAccount.GetProperty("accessToken").GetString()!;
    // Chỉ nâng quyền fixture trong DB QA ngẫu nhiên, không tạo admin mặc định cho ứng dụng.
    await AuthSql("UPDATE accounts SET role='admin' WHERE id=@id", adminId);
    var accounts = await Call("GET", "/api/auth/accounts", 200, token: adminToken);
    Check(!accounts.GetRawText().Contains("password", StringComparison.OrdinalIgnoreCase), "account DTO excludes hashes");
    if (catalogManagement || customerOrders || orderWorkflows)
    {
        var staffEmail = $"catalog-staff-{runId}@example.invalid";
        await Call("POST", "/api/auth/accounts", 201, new { email = staffEmail, password, fullName = "Catalog QA staff", phone = Phone(), role = "staff" }, adminToken);
        var staffLogin = await Call("POST", "/api/auth/login", 200, new { email = staffEmail, password });
        if (orderWorkflows)
        {
            var otherCustomer = await Call("POST", "/api/auth/register", 201,
                new { email = $"workflow-other-{runId}@example.invalid", password, fullName = "Workflow other", phone = Phone() });
            await OrderWorkflowChecks.RunAsync(http, managementConnection!, staffConnection!,
                (id, token), (otherCustomer.GetProperty("id").GetInt64(), otherCustomer.GetProperty("accessToken").GetString()!),
                adminToken, staffLogin.GetProperty("id").GetInt64(), staffLogin.GetProperty("accessToken").GetString()!,
                  () => Stop(staffProcess!), async () => { staffProcess = await Start("staff-service", "CleanHome.Staff.Api", staffPort, staffConnection); },staffPort,internalKey);
            if (reviewWorkflows)
                await ReviewChecks.RunAsync(http, token, otherCustomer.GetProperty("accessToken").GetString()!, adminToken, staffLogin.GetProperty("accessToken").GetString()!);
            return;
        }
        if (customerOrders)
        {
            var otherCustomer = await Call("POST", "/api/auth/register", 201,
                new { email = $"order-other-{runId}@example.invalid", password, fullName = "Other customer", phone = Phone() });
            await CustomerOrderChecks.RunAsync(http, managementConnection!, (id, token),
                (otherCustomer.GetProperty("id").GetInt64(), otherCustomer.GetProperty("accessToken").GetString()!),
                adminToken, staffLogin.GetProperty("accessToken").GetString()!);
            Stop(authProcess);
            await Call("GET", "/api/orders/me", 503, token: token);
            Pass("customer order API fails closed when auth-service is unavailable");
            return;
        }
        await CatalogManagementChecks.RunAsync(http, managementConnection!, adminToken, token, staffLogin.GetProperty("accessToken").GetString()!);
        Stop(authProcess);
        await Call("GET", "/api/catalog/services", 200, token: adminToken);
        await Call("GET", "/api/catalog/admin/services", 503, token: adminToken);
        Pass("catalog remains public with a stale bearer when auth is unavailable; admin fails closed");
        return;
    }

    var adminProfileRoute = $"/api/users/profiles/{id}";
    var originalProfile = await Call("GET", "/api/users/me", 200, token: token);
    await Call("GET", adminProfileRoute, 401);
    await Call("GET", $"/api/users/profiles/{long.MaxValue}", 401);
    await Call("GET", adminProfileRoute, 403, token: token);
    await Call("GET", $"/api/users/profiles/{long.MaxValue}", 403, token: token);
    await AuthSql("UPDATE accounts SET role='staff' WHERE id=@id", id);
    await Call("GET", adminProfileRoute, 403, token: token);
    await AuthSql("UPDATE accounts SET role='customer' WHERE id=@id", id);
    var adminRead = await Call("GET", adminProfileRoute, 200, token: adminToken, noStore: true);
    Check(adminRead.GetRawText() == originalProfile.GetRawText(), "admin reads the same persisted profile as its owner");
    Check(adminRead.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
        .SequenceEqual(new[] { "avatarUrl", "createdAt", "fullName", "phone", "updatedAt", "userId" }), "admin profile detail exposes only six profile fields");
    Check(adminRead.GetProperty("avatarUrl").ValueKind == JsonValueKind.Null, "missing avatar is returned as null");
    var readOwnProfile = await Call("GET", $"/api/users/profiles/{adminId}", 200, token: adminToken);
    Check(readOwnProfile.GetProperty("userId").GetInt64() == adminId && readOwnProfile.GetProperty("phone").GetString() == adminPhone,
        "admin can inspect own profile");
    await AuthSql("UPDATE accounts SET status='locked' WHERE id=@id", id);
    adminRead = await Call("GET", adminProfileRoute, 200, token: adminToken);
    Check(adminRead.GetRawText() == originalProfile.GetRawText(), "target account lock does not prevent admin reading existing profile");
    await AuthSql("UPDATE accounts SET status='active' WHERE id=@id", id);
    var afterRead = await Call("GET", "/api/users/me", 200, token: token);
    Check(afterRead.GetRawText() == originalProfile.GetRawText(), "profile GET does not mutate fields, timestamps or sessions");
    await Call("GET", "/api/users/profiles/0", 400, token: adminToken);
    await Call("GET", "/api/users/profiles/-1", 400, token: adminToken);
    await Call("GET", $"/api/users/profiles/{long.MaxValue}", 404, token: adminToken);
    await Call("GET", "/api/users/profiles/not-an-id", 404, token: adminToken);
    await Call("GET", "/api/users/profiles/9223372036854775808", 404, token: adminToken);
    var avatarFixture = await Call("PUT", "/api/users/me", 200,
        new { fullName = "Khách hàng kiểm thử", phone, avatarUrl = "https://example.invalid/detail-avatar.jpg" }, token);
    adminRead = await Call("GET", adminProfileRoute, 200, token: adminToken);
    Check(adminRead.GetRawText() == avatarFixture.GetRawText(), "admin reads the current avatar and update timestamp");
    originalProfile = await Call("PUT", "/api/users/me", 200, new { fullName = "Khách hàng kiểm thử", phone }, token);
    Pass("admin profile detail: permissions, locked targets, own profile, safe DTO, no-store, read-only behavior and invalid IDs");
    if (args.Contains("--admin-profile-detail-only", StringComparer.Ordinal))
    {
        Console.WriteLine("PASS: admin profile detail API checks completed.");
        return;
    }

    var adminProfileBody = new { fullName = "  Hồ sơ do admin sửa  ", phone, avatarUrl = "https://example.invalid/avatar.jpg" };
    await Call("PUT", adminProfileRoute, 401, adminProfileBody);
    await Call("PUT", adminProfileRoute, 403, adminProfileBody, token);
    await AuthSql("UPDATE accounts SET role='staff' WHERE id=@id", id);
    await Call("PUT", adminProfileRoute, 403, adminProfileBody, token);
    await AuthSql("UPDATE accounts SET role='customer' WHERE id=@id", id);
    await Call("PUT", adminProfileRoute, 400, new { fullName = " ", phone }, adminToken);
    await Call("PUT", adminProfileRoute, 400, new { fullName = "QA", phone = "abc" }, adminToken);
    await Call("PUT", adminProfileRoute, 400, new { fullName = "QA", phone, avatarUrl = "javascript:alert(1)" }, adminToken);
    await Call("PUT", adminProfileRoute, 400, new { fullName = "QA", phone, avatarUrl = new string('x', 2049) }, adminToken);
    await Call("PUT", adminProfileRoute, 400, new { fullName = "QA" }, adminToken);
    foreach (var invalidBody in new object[]
    {
        new { fullName = "QA", phone, userId = adminId }, new { fullName = "QA", phone, role = "admin" },
        new { fullName = "QA", phone, email }, new { fullName = "QA", phone, password }
    }) await Call("PUT", adminProfileRoute, 400, invalidBody, adminToken);
    await Call("PUT", "/api/users/profiles/0", 400, adminProfileBody, adminToken);
    await Call("PUT", "/api/users/profiles/-1", 400, adminProfileBody, adminToken);
    await Call("PUT", $"/api/users/profiles/{long.MaxValue}", 404, adminProfileBody, adminToken);
    await Call("PUT", adminProfileRoute, 409, new { fullName = "Must rollback", phone = adminPhone, avatarUrl = "https://example.invalid/wrong.jpg" }, adminToken);
    var unchanged = await Call("GET", "/api/users/me", 200, token: token);
    Check(unchanged.GetRawText() == originalProfile.GetRawText(), "duplicate phone rolls back all profile fields and timestamps");
    var adminUpdated = await Call("PUT", adminProfileRoute, 200, adminProfileBody, adminToken, noStore: true);
    Check(adminUpdated.GetProperty("userId").GetInt64() == id && adminUpdated.GetProperty("fullName").GetString() == "Hồ sơ do admin sửa" &&
        adminUpdated.GetProperty("phone").GetString() == phone && adminUpdated.GetProperty("avatarUrl").GetString() == adminProfileBody.avatarUrl &&
        adminUpdated.GetProperty("createdAt").GetString() == originalProfile.GetProperty("createdAt").GetString(), "admin updates only the target profile and preserves creation time");
    Check(adminUpdated.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
        .SequenceEqual(new[] { "avatarUrl", "createdAt", "fullName", "phone", "updatedAt", "userId" }), "admin profile response contains only profile DTO fields");
    var readUpdated = await Call("GET", "/api/users/me", 200, token: token);
    Check(readUpdated.GetRawText() == adminUpdated.GetRawText(), "updated profile is persisted and sessions remain valid");
    await AuthSql("UPDATE accounts SET status='locked' WHERE id=@id", id);
    await Call("PUT", adminProfileRoute, 200, adminProfileBody, adminToken);
    var lockedDetail = await Call("GET", $"/api/auth/accounts/{id}", 200, token: adminToken);
    Check(lockedDetail.GetProperty("status").GetString() == "locked" && lockedDetail.GetProperty("email").GetString() == email,
        "profile update does not unlock or change account credentials");
    await Call("GET", "/api/users/me", 401, token: token);
    await AuthSql("UPDATE accounts SET status='active' WHERE id=@id", id);
    var cleared = await Call("PUT", adminProfileRoute, 200, new { fullName = "Khách hàng kiểm thử", phone }, adminToken);
    Check(cleared.GetProperty("avatarUrl").ValueKind == JsonValueKind.Null, "omitted avatar clears the old URL with PUT semantics");
    var ownProfile = await Call("PUT", $"/api/users/profiles/{adminId}", 200,
        new { fullName = "QA administrator", phone = adminPhone }, adminToken);
    Check(ownProfile.GetProperty("userId").GetInt64() == adminId, "admin can update own profile");
    Pass("admin profile update: permissions, validation, duplicate rollback, persistence, DTO protection and locked account handling");
    if (args.Contains("--admin-profile-update-only", StringComparer.Ordinal))
    {
        Console.WriteLine("PASS: admin profile update API checks completed.");
        return;
    }

    // API chi tiết tài khoản: chỉ admin, không lộ thông tin xác thực hoặc lockout.
    var detailRoute = $"/api/auth/accounts/{id}";
    await Call("GET", detailRoute, 401);
    await Call("GET", $"/api/auth/accounts/{long.MaxValue}", 401);
    await Call("GET", detailRoute, 403, token: token);
    await Call("GET", $"/api/auth/accounts/{long.MaxValue}", 403, token: token);
    await AuthSql("UPDATE accounts SET role='staff' WHERE id=@id", id);
    await Call("GET", detailRoute, 403, token: token);
    await AuthSql("UPDATE accounts SET role='customer' WHERE id=@id", id);
    var detail = await Call("GET", detailRoute, 200, token: adminToken, noStore: true);
    Check(detail.GetProperty("id").GetInt64() == id && detail.GetProperty("email").GetString() == email &&
        detail.GetProperty("role").GetString() == "customer" && detail.GetProperty("status").GetString() == "active" &&
        detail.GetProperty("createdAt").TryGetDateTime(out _), "account detail maps the expected account fields");
    var publicFields = detail.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
    Check(publicFields.SequenceEqual(new[] { "createdAt", "email", "id", "role", "status" }), "account detail exposes only the safe response DTO");
    var ownDetail = await Call("GET", $"/api/auth/accounts/{adminId}", 200, token: adminToken);
    Check(ownDetail.GetProperty("id").GetInt64() == adminId && ownDetail.GetProperty("role").GetString() == "admin", "admin can view own account");
    await AuthSql("UPDATE accounts SET status='locked' WHERE id=@id", id);
    detail = await Call("GET", detailRoute, 200, token: adminToken);
    Check(detail.GetProperty("status").GetString() == "locked", "admin can inspect a locked account");
    await AuthSql("UPDATE accounts SET status='active' WHERE id=@id", id);
    await Call("GET", $"/api/auth/accounts/{long.MaxValue}", 404, token: adminToken);
    await Call("GET", "/api/auth/accounts/0", 400, token: adminToken);
    await Call("GET", "/api/auth/accounts/-1", 400, token: adminToken);
    await Call("GET", "/api/auth/accounts/not-an-id", 404, token: adminToken);
    await Call("GET", "/api/auth/accounts/9223372036854775808", 404, token: adminToken);
    Pass("admin account detail: permissions, active/locked accounts, safe DTO, no-store and missing/invalid IDs");
    if (args.Contains("--account-detail-only", StringComparer.Ordinal))
    {
        Console.WriteLine("PASS: account detail API checks completed.");
        return;
    }

    // API tìm kiếm tài khoản: kiểm tra quyền, filter, total, phân trang và ký tự đặc biệt.
    string SearchRoute(string value, string paging = "") => "/api/auth/accounts?search=" + Uri.EscapeDataString(value) + paging;
    await Call("GET", SearchRoute(email), 401);
    await Call("GET", SearchRoute(email), 403, token: token);
    var found = await Call("GET", SearchRoute("  " + email.ToUpperInvariant() + "  "), 200, token: adminToken);
    Check(found.GetProperty("total").GetInt64() == 1 && found.GetProperty("items")[0].GetProperty("id").GetInt64() == id,
        "email search ignores casing and surrounding whitespace");
    found = await Call("GET", SearchRoute(id.ToString()), 200, token: adminToken);
    Check(found.GetProperty("total").GetInt64() == 1 && found.GetProperty("items")[0].GetProperty("id").GetInt64() == id,
        "numeric search matches exact account ID");
    var firstPage = await Call("GET", SearchRoute(runId, "&page=1&pageSize=1"), 200, token: adminToken);
    var secondPage = await Call("GET", SearchRoute(runId, "&page=2&pageSize=1"), 200, token: adminToken);
    Check(firstPage.GetProperty("total").GetInt64() == 2 && secondPage.GetProperty("total").GetInt64() == 2 &&
        firstPage.GetProperty("items").GetArrayLength() == 1 && secondPage.GetProperty("items").GetArrayLength() == 1 &&
        firstPage.GetProperty("items")[0].GetProperty("id").GetInt64() == adminId &&
        secondPage.GetProperty("items")[0].GetProperty("id").GetInt64() == id, "filtered pagination has consistent total and descending IDs");
    found = await Call("GET", SearchRoute(runId, "&page=3&pageSize=1"), 200, token: adminToken);
    Check(found.GetProperty("total").GetInt64() == 2 && found.GetProperty("items").GetArrayLength() == 0, "page beyond results retains filtered total");
    found = await Call("GET", SearchRoute("  "), 200, token: adminToken);
    Check(found.GetProperty("total").GetInt64() == accounts.GetProperty("total").GetInt64(), "blank keyword preserves original listing");
    foreach (var keyword in new[] { "missing-" + runId, "' OR 1=1 --", "%" })
    {
        found = await Call("GET", SearchRoute(keyword), 200, token: adminToken);
        Check(found.GetProperty("total").GetInt64() == 0 && found.GetProperty("items").GetArrayLength() == 0, "literal/unmatched keywords return empty results");
    }
    await Call("GET", "/api/auth/accounts?page=0", 400, token: adminToken);
    await Call("GET", "/api/auth/accounts?pageSize=101", 400, token: adminToken);
    await Call("GET", SearchRoute(new string('x', 255)), 400, token: adminToken);
    var literalAccount = await Call("POST", "/api/auth/register", 201,
        new { email = $"literal_%!-{runId}@example.invalid", password, fullName = "QA literal email", phone = Phone() });
    found = await Call("GET", SearchRoute("_%!"), 200, token: adminToken);
    Check(found.GetProperty("total").GetInt64() == 1 && found.GetProperty("items")[0].GetProperty("id").GetInt64() == literalAccount.GetProperty("id").GetInt64(),
        "percent, underscore and escape character match literally");
    Check(!found.GetRawText().Contains("password", StringComparison.OrdinalIgnoreCase) && !found.GetRawText().Contains("token", StringComparison.OrdinalIgnoreCase),
        "search response excludes credentials");
    Pass("admin account search: email/ID, pagination, empty results, validation, permissions and literal SQL characters");
    if (args.Contains("--accounts-search-only", StringComparer.Ordinal))
    {
        Console.WriteLine("PASS: account search API checks completed.");
        return;
    }

    // API admin cập nhật email: bảo vệ DTO, uniqueness, transaction và thu hồi phiên.
    var updateRoute = $"/api/auth/accounts/{id}";
    await Call("PUT", updateRoute, 401, new { email });
    await Call("PUT", updateRoute, 403, new { email }, token);
    await AuthSql("UPDATE accounts SET role='staff' WHERE id=@id", id);
    await Call("PUT", updateRoute, 403, new { email }, token);
    await AuthSql("UPDATE accounts SET role='customer' WHERE id=@id", id);
    await Call("PUT", updateRoute, 400, new { }, adminToken);
    await Call("PUT", updateRoute, 400, new { email = "invalid" }, adminToken);
    await Call("PUT", updateRoute, 400, new { email = new string('a', 250) + "@example.com" }, adminToken);
    foreach (var invalidBody in new object[]
    {
        new { email, role = "admin" }, new { email, status = "active" },
        new { email, password = changedPassword }, new { email, fullName = "Unexpected" }, new { email, phone }
    }) await Call("PUT", updateRoute, 400, invalidBody, adminToken);
    await Call("PUT", "/api/auth/accounts/0", 400, new { email }, adminToken);
    await Call("PUT", "/api/auth/accounts/-1", 400, new { email }, adminToken);
    await Call("PUT", $"/api/auth/accounts/{long.MaxValue}", 404, new { email }, adminToken);
    await Call("PUT", $"/api/auth/accounts/{adminId}", 409, new { email = adminEmail }, adminToken);
    await Call("PUT", updateRoute, 409, new { email = adminEmail.ToUpperInvariant() }, adminToken);
    await Call("GET", "/api/auth/session", 200, token: token);
    detail = await Call("GET", updateRoute, 200, token: adminToken);
    Check(detail.GetProperty("email").GetString() == email, "duplicate email rolls back account update and preserves sessions");
    await Call("PUT", updateRoute, 200, new { email = email.ToUpperInvariant() }, adminToken, noStore: true);
    await Call("GET", "/api/auth/session", 200, token: token);
    var parallelLogin = await Call("POST", "/api/auth/login", 200, new { email, password });
    var parallelToken = parallelLogin.GetProperty("accessToken").GetString()!;
    var editedEmail = $"edited-{runId}@example.invalid";
    var edited = await Call("PUT", updateRoute, 200, new { email = editedEmail.ToUpperInvariant() }, adminToken, noStore: true);
    Check(edited.GetProperty("email").GetString() == editedEmail && edited.GetProperty("role").GetString() == "customer" &&
        edited.GetProperty("status").GetString() == "active" && edited.EnumerateObject().Count() == 5, "update returns normalized email and safe account DTO");
    await Call("GET", "/api/auth/session", 401, token: token);
    await Call("GET", "/api/users/me", 401, token: parallelToken);
    await Call("POST", "/api/auth/login", 401, new { email, password });
    var editedLogin = await Call("POST", "/api/auth/login", 200, new { email = editedEmail, password });
    var editedToken = editedLogin.GetProperty("accessToken").GetString()!;
    await Call("PUT", updateRoute, 200, new { email = editedEmail }, adminToken);
    await Call("GET", "/api/auth/session", 200, token: editedToken);
    await AuthSql("UPDATE accounts SET status='locked' WHERE id=@id", id);
    edited = await Call("PUT", updateRoute, 200, new { email }, adminToken);
    Check(edited.GetProperty("status").GetString() == "locked", "email edit does not unlock the target account");
    await Call("POST", "/api/auth/login", 401, new { email, password });
    await AuthSql("UPDATE accounts SET status='active' WHERE id=@id", id);
    login = await Call("POST", "/api/auth/login", 200, new { email, password });
    token = login.GetProperty("accessToken").GetString()!;
    var editableStaff = await Call("POST", "/api/auth/accounts", 201,
        new { email = $"editable-staff-{runId}@example.invalid", password, fullName = "QA editable staff", phone = Phone(), role = "staff" }, adminToken);
    var editedStaffEmail = $"edited-staff-{runId}@example.invalid";
    edited = await Call("PUT", "/api/auth/accounts/" + editableStaff.GetProperty("id").GetInt64(), 200, new { email = editedStaffEmail }, adminToken);
    Check(edited.GetProperty("role").GetString() == "staff", "admin can edit staff email without changing role");
    await Call("POST", "/api/auth/login", 200, new { email = editedStaffEmail, password });
    Pass("admin account update: validation, permissions, duplicate rollback, no-op, session revocation and preserved role/status/password");
    if (args.Contains("--account-update-only", StringComparer.Ordinal))
    {
        Console.WriteLine("PASS: account update API checks completed.");
        return;
    }

    await Call("PUT", $"/api/auth/accounts/{id}/status", 400, new { }, adminToken);
    await Call("PUT", $"/api/auth/accounts/{adminId}/status", 409, new { locked = true }, adminToken);
    await Call("PUT", $"/api/auth/accounts/{id}/status", 204, new { locked = true }, adminToken);
    await Call("GET", "/api/users/me", 401, token: token);
    await Call("PUT", $"/api/auth/accounts/{id}/status", 204, new { locked = false }, adminToken);
    login = await Call("POST", "/api/auth/login", 200, new { email, password });
    token = login.GetProperty("accessToken").GetString()!;
    var staff = await Call("POST", "/api/auth/accounts", 201,
        new { email = $"staff-{runId}@example.invalid", password, fullName = "QA staff", phone = Phone(), role = "staff" }, adminToken);
    Check(staff.GetProperty("role").GetString() == "staff" && staff.GetProperty("accessToken").ValueKind == JsonValueKind.Null, "admin creates staff without impersonation token");
    Pass("admin account creation, lock/unlock, DTO filtering and immediate session revocation");

    var addressBody = new { recipient = "Người nhận", phone, addressText = "123 Đường kiểm thử", latitude = 10.75, longitude = 106.65 };
    var address = await Call("POST", "/api/users/me/addresses", 201, addressBody, token);
    var addressId = address.GetProperty("id").GetInt64();
    await Call("GET", $"/api/users/me/addresses/{addressId}", 200, token: token);
    await Call("GET", $"/api/users/me/addresses/{addressId}", 404, token: adminToken);
    await Call("PUT", $"/api/users/me/addresses/{addressId}", 404, addressBody, adminToken);
    await Call("DELETE", $"/api/users/me/addresses/{addressId}", 404, token: adminToken);
    await Call("POST", "/api/users/me/addresses", 400, new { recipient = "QA", phone, addressText = "123 Test", latitude = 100, longitude = 0 }, token);
    await Call("PUT", $"/api/users/me/addresses/{addressId}", 204, addressBody, token);
    await Call("PUT", "/api/users/me", 409, new { fullName = "Tên mới", phone = adminPhone }, token);
    await Call("PUT", "/api/users/me", 400, new { fullName = "Tên mới", phone, avatarUrl = "javascript:alert(1)" }, token);
    var saved = await Call("PUT", "/api/users/me", 200, new { fullName = "Tên đã cập nhật", phone, userId = adminId }, token);
    Check(saved.GetProperty("userId").GetInt64() == id, "profile owner comes from authenticated identity");
    await Call("DELETE", $"/api/users/me/addresses/{addressId}", 204, token: token);
    await Call("GET", $"/api/users/me/addresses/{addressId}", 404, token: token);
    Pass("profile/address CRUD, validation and ownership protection");

    var provision = new { userId = id, fullName = "Must not overwrite", phone };
    await Call("POST", "/api/users/internal/profiles", 404, provision, extraKey: internalKey);
    await Call("POST", $"http://127.0.0.1:{userPort}/internal/profiles", 403, provision);
    await Call("POST", $"http://127.0.0.1:{userPort}/internal/profiles", 204, provision, extraKey: internalKey);
    profile = await Call("GET", "/api/users/me", 200, token: token);
    Check(profile.GetProperty("fullName").GetString() == "Tên đã cập nhật", "duplicate delivery does not overwrite profile");
    Pass("internal key, gateway isolation and idempotent provisioning");

    await Call("PUT", "/api/auth/password", 400, new { currentPassword = "WrongPassword", newPassword = changedPassword }, token);
    await Call("PUT", "/api/auth/password", 204, new { currentPassword = password, newPassword = changedPassword }, token);
    await Call("GET", "/api/auth/session", 401, token: token);
    await Call("POST", "/api/auth/login", 401, new { email, password });
    login = await Call("POST", "/api/auth/login", 200, new { email, password = changedPassword });
    token = login.GetProperty("accessToken").GetString()!;
    var newEmail = $"changed-{runId}@example.invalid";
    await Call("PUT", "/api/auth/email", 204, new { email = newEmail, password = changedPassword }, token);
    await Call("GET", "/api/users/me", 401, token: token);
    login = await Call("POST", "/api/auth/login", 200, new { email = newEmail, password = changedPassword });
    token = login.GetProperty("accessToken").GetString()!;
    var anotherLogin = await Call("POST", "/api/auth/login", 200, new { email = newEmail, password = changedPassword });
    var otherToken = anotherLogin.GetProperty("accessToken").GetString()!;
    await Call("POST", "/api/auth/logout", 204, token: token);
    await Call("GET", "/api/auth/session", 401, token: token);
    await Call("GET", "/api/auth/session", 200, token: otherToken);
    await Call("POST", "/api/auth/logout-all", 204, token: otherToken);
    await Call("GET", "/api/users/me", 401, token: otherToken);
    Pass("password/email changes and single/all-session logout");

    Stop(userProcess);
    var pendingEmail = $"pending-{runId}@example.invalid";
    var pending = await Call("POST", "/api/auth/register", 201, new { email = pendingEmail, password, fullName = "Outbox retry", phone = Phone() });
    Check(!pending.GetProperty("profileReady").GetBoolean(), "registration reports pending profile when user service is down");
    var pendingToken = pending.GetProperty("accessToken").GetString()!;
    await Start("user-service", "CleanHome.User.Api", userPort, userConnection);
    var recovered = false;
    var retryDeadline = DateTime.UtcNow.AddSeconds(40);
    while (DateTime.UtcNow < retryDeadline)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pendingToken);
        using var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode) { recovered = true; break; }
        await Task.Delay(500);
    }
    Check(recovered, "durable outbox recovers after downstream restart");
    Stop(authProcess);
    await Call("GET", "/api/users/me", 503, token: pendingToken);
    Pass("outbox retry and authentication outage fail closed");
    Console.WriteLine("PASS: all auth/user integration checks completed; no application database was used.");
}
finally
{
    foreach (var process in processes) { Stop(process); process.Dispose(); }
    await using var admin = new MySqlConnection(adminSettings.ConnectionString);
    await admin.OpenAsync();
    foreach (var name in databases)
    {
        if (!Regex.IsMatch(name, "^cleanhome_test_[a-f0-9]{12}_(auth|user|service|order|staff|review)$")) throw new InvalidOperationException("Unsafe QA database name.");
        await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + name + "`", admin);
        await drop.ExecuteNonQueryAsync();
    }
    Console.WriteLine("QA databases removed. Service logs retained in TEMP; credentials/tokens are not printed.");
}

async Task<Process> Start(string service, string assembly, int port, string? connection)
{
    var directory = Path.Combine(backend, service, "src");
    var info = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    info.ArgumentList.Add(Path.Combine(directory, "bin", configuration, "net10.0", assembly + ".dll"));
    info.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
    info.Environment["InternalApi__Key"] = internalKey;
    info.Environment["Services__AuthUrl"] = $"http://127.0.0.1:{authPort}";
    info.Environment["Services__UserUrl"] = $"http://127.0.0.1:{userPort}";
    info.Environment["Services__StaffUrl"] = $"http://127.0.0.1:{staffPort}";
    info.Environment["Services__OrderUrl"] = $"http://127.0.0.1:{catalogPort}";
    info.Environment["Authentication__RequestsPerMinute"] = "1000";
    info.Environment["ConnectionStrings__Database"] = connection ?? "";
    info.Environment["ReverseProxy__Clusters__auth__Destinations__local__Address"] = $"http://127.0.0.1:{authPort}/";
    info.Environment["ReverseProxy__Clusters__users__Destinations__local__Address"] = $"http://127.0.0.1:{userPort}/";
    info.Environment["ReverseProxy__Clusters__catalog__Destinations__local__Address"] = $"http://127.0.0.1:{catalogPort}/";
    info.Environment["ReverseProxy__Clusters__orders__Destinations__local__Address"] = $"http://127.0.0.1:{catalogPort}/";
    info.Environment["ReverseProxy__Clusters__staff__Destinations__local__Address"] = $"http://127.0.0.1:{staffPort}/";
    info.Environment["ReverseProxy__Clusters__reviews__Destinations__local__Address"] = $"http://127.0.0.1:{reviewPort}/";
    var process = new Process { StartInfo = info };
    var logPath = Path.Combine(logDirectory, service + "-" + Guid.NewGuid().ToString("N")[..6] + ".log");
    var logLock = new object();
    void Log(object? sender, DataReceivedEventArgs args)
    {
        if (args.Data is not null) lock (logLock) File.AppendAllText(logPath, args.Data + Environment.NewLine);
    }
    process.OutputDataReceived += Log; process.ErrorDataReceived += Log;
    process.Start(); processes.Add(process); process.BeginOutputReadLine(); process.BeginErrorReadLine();
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    var deadline = DateTime.UtcNow.AddSeconds(20);
    while (DateTime.UtcNow < deadline)
    {
        if (process.HasExited) throw new InvalidOperationException($"{service} exited; see {logPath}.");
        try { using var response = await probe.GetAsync($"http://127.0.0.1:{port}/health/live"); if (response.IsSuccessStatusCode) return process; }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(100);
    }
    throw new InvalidOperationException($"{service} did not start; see {logPath}.");
}

async Task<JsonElement> Call(string method, string route, int expected, object? body = null, string? token = null, string? extraKey = null, bool noStore = false)
{
    using var request = new HttpRequestMessage(new HttpMethod(method), route);
    if (body is not null) request.Content = JsonContent.Create(body);
    if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (extraKey is not null) request.Headers.Add("X-CleanHome-Service-Key", extraKey);
    using var response = await http.SendAsync(request);
    if ((int)response.StatusCode != expected)
        throw new InvalidOperationException($"{method} {route}: expected HTTP {expected}, got {(int)response.StatusCode}. Logs: {logDirectory}");
    if (noStore) Check(response.Headers.CacheControl?.NoStore == true, "sensitive account detail must not be cached");
    var content = await response.Content.ReadAsStringAsync();
    if (string.IsNullOrEmpty(content)) return default;
    using var document = JsonDocument.Parse(content);
    return document.RootElement.Clone();
}

async Task AuthSql(string sql, long id)
{
    await using var connection = new MySqlConnection(authConnection);
    await connection.OpenAsync();
    await using var command = connection.Sql(sql, ("id", id));
    await command.ExecuteNonQueryAsync();
}

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + description);
}
static void Pass(string description) => Console.WriteLine("PASS: " + description);
static int FreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
}
static void Stop(Process process)
{
    if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
}
