using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

if (!args.Contains("--local-demo")) throw new InvalidOperationException("Chỉ chạy với --local-demo trên XAMPP local.");
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
if (!File.Exists(Path.Combine(root, "CleanHome.slnx"))) throw new InvalidOperationException("Không tìm thấy backend workspace.");
var privateDir = Path.Combine(root, ".local");
Directory.CreateDirectory(privateDir);
var credentialPath = Path.Combine(privateDir, "demo-accounts.json");
var credentials = File.Exists(credentialPath) ? JsonSerializer.Deserialize<List<Credential>>(await File.ReadAllTextAsync(credentialPath))! : [];
// Deliberately fixed local host and database names: this tool cannot target production.
await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Character Set=utf8mb4;DateTimeKind=Utc");
await connection.OpenAsync();
await using var lockCommand = new MySqlCommand("SELECT GET_LOCK('cleanhome_local_demo_seed', 5)", connection);
if (Convert.ToInt32(await lockCommand.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Seed khác đang chạy.");
await using var transaction = await connection.BeginTransactionAsync();
async Task<long> Scalar(string sql, params (string, object?)[] values)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    var valueResult = await command.ExecuteScalarAsync();
    return valueResult is null or DBNull ? 0 : Convert.ToInt64(valueResult);
}
async Task Execute(string sql, params (string, object?)[] values)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    await command.ExecuteNonQueryAsync();
}
async Task<long> Ensure(string table, string where, (string, object?)[] key, string columns, string parameters, params (string, object?)[] values)
{
    var id = await Scalar($"SELECT id FROM {table} WHERE {where} LIMIT 1", key);
    if (id > 0) return id;
    await Execute($"INSERT INTO {table} ({columns}) VALUES ({parameters})", values);
    return await Scalar("SELECT LAST_INSERT_ID()");
}
var hasher = new PasswordHasher<string>();
async Task<long> Account(string role, string suffix, string name, string phone)
{
    var email = $"demo.{suffix}@cleanhome.local";
    var existing = await Scalar("SELECT id FROM cleanhome_auth.accounts WHERE email=@email", ("@email", email));
    long id;
    if (existing > 0)
    {
        var roleMatches = await Scalar("SELECT COUNT(*) FROM cleanhome_auth.accounts WHERE id=@id AND role=@role", ("@id", existing), ("@role", role));
        if (roleMatches != 1) throw new InvalidOperationException("Email seed đã có vai trò khác; không sửa tài khoản cũ.");
        id = existing;
    }
    else
    {
        var password = "CH!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
        await Execute("INSERT INTO cleanhome_auth.accounts(email,password_hash,role,status) VALUES(@email,@hash,@role,'active')", ("@email", email), ("@hash", hasher.HashPassword(email, password)), ("@role", role));
        id = await Scalar("SELECT LAST_INSERT_ID()");
        credentials.Add(new(role, email, password));
    }
    await Execute("INSERT INTO cleanhome_user.profiles(user_id,full_name,phone) SELECT @id,@name,@phone WHERE NOT EXISTS(SELECT 1 FROM cleanhome_user.profiles WHERE user_id=@id)", ("@id", id), ("@name", name), ("@phone", phone));
    return id;
}
var admin = await Account("admin", "admin", "Quản trị CleanHome mẫu", "0998800001");
var customer = await Account("customer", "customer", "Nguyễn Minh Anh (mẫu)", "0998800002");
var staffUsers = new[] { await Account("staff", "staff", "Trần Văn Hùng (mẫu)", "0998800003"), await Account("staff", "staff2", "Lê Thị Mai (mẫu)", "0998800004"), await Account("staff", "staff3", "Phạm Quốc Bảo (mẫu)", "0998800005") };
await Execute("INSERT INTO cleanhome_user.addresses(user_id,recipient,phone,address_text,latitude,longitude) SELECT @user,'Nguyễn Minh Anh (mẫu)','0998800002','Địa chỉ mẫu: 123 Nguyễn Thị Minh Khai, TP.HCM',10.7769,106.7009 WHERE NOT EXISTS(SELECT 1 FROM cleanhome_user.addresses WHERE user_id=@user)", ("@user", customer));
var branch = await Ensure("cleanhome_staff.branches", "name=@name", [("@name", "CleanHome TP.HCM (mẫu)")], "name,address_text,service_area,latitude,longitude,status", "@name,@address,@area,10.7769,106.7009,'active'", ("@name", "CleanHome TP.HCM (mẫu)"), ("@address", "Chi nhánh mẫu tại TP.HCM"), ("@area", "{\"city\":\"TP.HCM\",\"sample\":true}"));
var skill = await Ensure("cleanhome_staff.skills", "name=@name", [("@name", "Vệ sinh nhà ở (mẫu)")], "name,description", "@name,@description", ("@name", "Vệ sinh nhà ở (mẫu)"), ("@description", "Kỹ năng mẫu để thử phân công nhân viên."));
var employees = new List<long>();
foreach (var user in staffUsers)
{
    var employee = await Ensure("cleanhome_staff.employees", "user_id=@user", [("@user", user)], "user_id,branch_id,verification_status,experience_years,response_rate,status", "@user,@branch,'verified',3,100,'active'", ("@user", user), ("@branch", branch));
    employees.Add(employee);
    await Execute("INSERT INTO cleanhome_staff.employee_skills(employee_id,skill_id,level) SELECT @employee,@skill,'qualified' WHERE NOT EXISTS(SELECT 1 FROM cleanhome_staff.employee_skills WHERE employee_id=@employee AND skill_id=@skill)", ("@employee", employee), ("@skill", skill));
    for (var day = -3; day <= 14; day++)
    {
        var start = DateTime.UtcNow.Date.AddDays(day).AddHours(1); // 08:00 UTC+7.
        await Execute("INSERT INTO cleanhome_staff.employee_availability(employee_id,starts_at,ends_at,status) SELECT @employee,@start,@end,'available' WHERE NOT EXISTS(SELECT 1 FROM cleanhome_staff.employee_availability WHERE employee_id=@employee AND starts_at=@start AND ends_at=@end)", ("@employee", employee), ("@start", start), ("@end", start.AddHours(10)));
    }
}
var categoryNames = new[] { "Dọn dẹp nhà ở", "Vệ sinh chuyên sâu", "Vệ sinh nội thất", "Vệ sinh thiết bị" };
var categories = new List<long>();
foreach (var name in categoryNames)
    categories.Add(await Ensure("cleanhome_service.service_categories", "name=@name", [("@name", name)], "name,description,is_active", "@name,@description,1", ("@name", name), ("@description", $"Danh mục {name.ToLowerInvariant()} — dữ liệu mẫu CleanHome.")));
var definitions = new[] {
    new ServiceSeed("Dọn nhà theo giờ",0,"giờ",75000,60,"Lau dọn phòng, quét và lau sàn, vệ sinh bếp và phòng tắm. Giá mẫu theo giờ."),
    new ServiceSeed("Tổng vệ sinh nhà",1,"gói",650000,240,"Vệ sinh tổng thể căn hộ, khu vực sinh hoạt, bếp, cửa kính và phòng tắm. Dịch vụ mẫu."),
    new ServiceSeed("Vệ sinh sofa",2,"bộ",350000,120,"Hút bụi, làm sạch và xử lý vết bẩn bề mặt sofa. Dịch vụ mẫu."),
    new ServiceSeed("Vệ sinh máy lạnh",3,"máy",180000,60,"Làm sạch bộ lọc, dàn lạnh và kiểm tra thoát nước. Dịch vụ mẫu."),
    new ServiceSeed("Vệ sinh sau xây dựng",1,"gói",1200000,360,"Thu gom bụi, làm sạch sàn và bề mặt sau thi công. Dịch vụ mẫu."),
    new ServiceSeed("Vệ sinh nệm",2,"tấm",250000,90,"Hút bụi và vệ sinh nệm bằng thiết bị chuyên dụng. Dịch vụ mẫu.") };
var services = new List<long>();
foreach (var item in definitions)
    services.Add(await Ensure("cleanhome_service.services", "name=@name", [("@name", item.Name)], "category_id,name,description,unit,base_price,duration_minutes,is_active", "@category,@name,@description,@unit,@price,@duration,1", ("@category", categories[item.Category]), ("@name", item.Name), ("@description", item.Description), ("@unit", item.Unit), ("@price", item.Price), ("@duration", item.Minutes)));

var bookingDefinitions = new[] { ("CH-SAMPLE-001", "pending", 3, 0, 3m), ("CH-SAMPLE-002", "confirmed", 2, 1, 1m), ("CH-SAMPLE-003", "completed", -2, 2, 1m), ("CH-SAMPLE-004", "cancelled", -1, 3, 1m) };
long completedBooking = 0, completedAssignment = 0;
foreach (var (code, status, day, serviceIndex, quantity) in bookingDefinitions)
{
    // Never reconstruct or overwrite a sample booking after a user has edited it.
    var existing = await Scalar("SELECT id FROM cleanhome_order.bookings WHERE code=@code", ("@code", code));
    if (existing > 0) continue;
    var item = definitions[serviceIndex];
    var start = DateTime.UtcNow.Date.AddDays(day).AddHours(2);
    var end = start.AddMinutes(item.Minutes * (double)quantity);
    var amount = item.Price * quantity;
    var address = JsonSerializer.Serialize(new { recipient = "Nguyễn Minh Anh (mẫu)", phone = "0998800002", addressText = "Địa chỉ mẫu: 123 Nguyễn Thị Minh Khai, TP.HCM", latitude = 10.7769, longitude = 106.7009 });
    await Execute("INSERT INTO cleanhome_order.bookings(customer_id,branch_id,address_snapshot,scheduled_start,scheduled_end,status,total_amount,code,special_request,subtotal_amount,discount_amount) VALUES(@customer,@branch,@address,@start,@end,@status,@amount,@code,'Đơn mẫu để thử giao diện, không phải yêu cầu dịch vụ thực.',@amount,0)", ("@customer", customer), ("@branch", branch), ("@address", address), ("@start", start), ("@end", end), ("@status", status), ("@amount", amount), ("@code", code));
    var booking = await Scalar("SELECT LAST_INSERT_ID()");
    await Execute("INSERT INTO cleanhome_order.booking_items(booking_id,service_id,service_name_snapshot,quantity,unit_price,unit_snapshot) VALUES(@booking,@service,@name,@quantity,@price,@unit)", ("@booking", booking), ("@service", services[serviceIndex]), ("@name", item.Name), ("@quantity", quantity), ("@price", item.Price), ("@unit", item.Unit));
    foreach (var history in status switch { "completed" => new[] { "pending", "confirmed", "in_progress", "completed" }, "confirmed" => ["pending", "confirmed"], "cancelled" => ["pending", "cancelled"], _ => ["pending"] })
        await Execute("INSERT INTO cleanhome_order.booking_status_history(booking_id,changed_by,status,note,created_at) VALUES(@booking,@actor,@status,'Trạng thái minh họa được tạo bởi local seed.',@at)", ("@booking", booking), ("@actor", admin), ("@status", history), ("@at", start.AddDays(-1).AddMinutes(Array.IndexOf(new[] { "pending", "confirmed", "in_progress", "completed", "cancelled" }, history))));
    if (status is "confirmed" or "completed")
        foreach (var employee in employees.Take(2))
        {
            await Execute("INSERT INTO cleanhome_staff.assignments(booking_id,employee_id,assigned_at,check_in_at,check_out_at,status,scheduled_start,scheduled_end,assigned_by) VALUES(@booking,@employee,@assigned,@checkin,@checkout,@status,@start,@end,@admin)", ("@booking", booking), ("@employee", employee), ("@assigned", start.AddDays(-1)), ("@checkin", status == "completed" ? start : null), ("@checkout", status == "completed" ? end : null), ("@status", status == "completed" ? "done" : "waiting"), ("@start", start), ("@end", end), ("@admin", admin));
            if (status == "completed" && completedAssignment == 0) { completedAssignment = await Scalar("SELECT LAST_INSERT_ID()"); completedBooking = booking; }
        }
}
if (completedBooking > 0)
    await Execute("INSERT INTO cleanhome_review.reviews(booking_id,rating,comment,created_at,employee_id,customer_id,assignment_id) VALUES(@booking,5,'Đánh giá mẫu: nhân viên làm việc đúng giờ, cẩn thận và thân thiện.',UTC_TIMESTAMP(6),@employee,@customer,@assignment)", ("@booking", completedBooking), ("@employee", employees[0]), ("@customer", customer), ("@assignment", completedAssignment));
// Save generated credentials locally before commit; never print hashes/passwords to logs.
await File.WriteAllTextAsync(credentialPath, JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true }));
await transaction.CommitAsync();
Console.WriteLine("Seed local hoàn tất. Không sửa/xóa bản ghi đã tồn tại; không tạo thanh toán giả.");
Console.WriteLine("Tài khoản và mật khẩu sinh ngẫu nhiên: backend/.local/demo-accounts.json (Git ignored).");
record Credential(string Role, string Email, string Password);
record ServiceSeed(string Name, int Category, string Unit, decimal Price, int Minutes, string Description);
