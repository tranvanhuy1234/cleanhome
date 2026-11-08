using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using CleanHome.Staff.Api.Interfaces;
using CleanHome.Staff.Api.Models;
using MySqlConnector;

namespace CleanHome.Staff.Api.Repositories;

public sealed partial class StaffRepository(DatabaseConnectionFactory connections,IActiveStaffClient activeStaff) : IStaffRepository,IDispatchRepository
{
    private const string EmployeeColumns = "id,user_id,branch_id,verification_status,experience_years,response_rate,status";
    private const string AssignmentSelect = "SELECT a.id,a.booking_id,a.employee_id,e.user_id,a.status,a.scheduled_start,a.scheduled_end,a.check_in_at,a.check_out_at,a.version FROM assignments a JOIN employees e ON e.id=a.employee_id";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static EmployeeRecord Employee(MySqlDataReader r) => new(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetString(3), r.GetInt32(4), r.GetDecimal(5), r.GetString(6));
    private static BranchRecord Branch(MySqlDataReader r)
    {
        using var area = JsonDocument.Parse(r.GetString(3));
        return new(r.GetInt64(0), r.GetString(1), r.GetString(2), area.RootElement.Clone(), r.GetDecimal(4), r.GetDecimal(5), r.GetString(6));
    }
    private static AssignmentSnapshot Assignment(MySqlDataReader r) => new(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetString(4),
        r.GetDateTime(5), r.GetDateTime(6), r.IsDBNull(7) ? null : r.GetDateTime(7), r.IsDBNull(8) ? null : r.GetDateTime(8), r.GetInt64(9));
    private static MySqlCommand Cmd(MySqlConnection c, MySqlTransaction t, string sql, params (string, object?)[] values)
    { var command = c.Sql(sql, values); command.Transaction = t; return command; }

    public Task<(IReadOnlyList<BranchRecord> Items, long Total)> BranchesAsync(int page, int size, CancellationToken ct) =>
        PageAsync("SELECT id,name,address_text,service_area,latitude,longitude,status FROM branches", "SELECT COUNT(*) FROM branches", page, size, Branch, ct);
    public async Task<BranchRecord?> BranchAsync(long id, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        await using var q = c.Sql("SELECT id,name,address_text,service_area,latitude,longitude,status FROM branches WHERE id=@id", ("id", id));
        await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Branch(r) : null;
    }
    public async Task<long> SaveBranchAsync(BranchRecord item, bool create, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        if (!create && await BranchAsync(item.Id, ct) is null) throw new ApiException(404, "Không tìm thấy chi nhánh.");
        await using var q = c.Sql(create
            ? "INSERT INTO branches (name,address_text,service_area,latitude,longitude,status) VALUES (@name,@address,@area,@lat,@lng,@status)"
            : "UPDATE branches SET name=@name,address_text=@address,service_area=@area,latitude=@lat,longitude=@lng,status=@status WHERE id=@id",
            ("id", item.Id), ("name", item.Name), ("address", item.AddressText), ("area", item.ServiceArea.GetRawText()), ("lat", item.Latitude), ("lng", item.Longitude), ("status", item.Status));
        await q.ExecuteNonQueryAsync(ct); return create ? q.LastInsertedId : item.Id;
    }
    public Task<(IReadOnlyList<EmployeeRecord> Items, long Total)> EmployeesAsync(int page, int size, long? branchId, CancellationToken ct) =>
        PageAsync($"SELECT {EmployeeColumns} FROM employees WHERE (@branch IS NULL OR branch_id=@branch)",
            "SELECT COUNT(*) FROM employees WHERE (@branch IS NULL OR branch_id=@branch)", page, size, Employee, ct, ("branch", branchId));
    public Task<EmployeeRecord?> EmployeeAsync(long id, CancellationToken ct) => FindEmployeeAsync("id", id, ct);
    public Task<EmployeeRecord?> EmployeeByUserAsync(long userId, CancellationToken ct) => FindEmployeeAsync("user_id", userId, ct);
    private async Task<EmployeeRecord?> FindEmployeeAsync(string column, long id, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        await using var q = c.Sql($"SELECT {EmployeeColumns} FROM employees WHERE {column}=@id", ("id", id));
        await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Employee(r) : null;
    }
    public async Task<long> SaveEmployeeAsync(EmployeeRecord item, bool create, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        if (!create)
        {
            var old = await LockEmployee(c, t, item.Id, ct);
            if (old.BranchId != item.BranchId || item.Status != "active" || item.VerificationStatus != "verified")
                await NoActiveAssignments(c, t, item.Id, ct);
        }
        await using var q = Cmd(c, t, create
            ? "INSERT INTO employees (user_id,branch_id,verification_status,experience_years,response_rate,status) VALUES (@user,@branch,@verification,@years,0,@status)"
            : "UPDATE employees SET branch_id=@branch,verification_status=@verification,experience_years=@years,status=@status WHERE id=@id",
            ("id", item.Id), ("user", item.UserId), ("branch", item.BranchId), ("verification", item.VerificationStatus), ("years", item.ExperienceYears), ("status", item.Status));
        await q.ExecuteNonQueryAsync(ct); var id = create ? q.LastInsertedId : item.Id; await t.CommitAsync(ct); return id;
    }
    public async Task<IReadOnlyList<AvailabilityRecord>> AvailabilityAsync(long employeeId, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        await using var q = c.Sql("SELECT id,employee_id,starts_at,ends_at,status FROM employee_availability WHERE employee_id=@id ORDER BY starts_at,id", ("id", employeeId));
        await using var r = await q.ExecuteReaderAsync(ct); var rows = new List<AvailabilityRecord>();
        while (await r.ReadAsync(ct)) rows.Add(new(r.GetInt64(0), r.GetInt64(1), r.GetDateTime(2), r.GetDateTime(3), r.GetString(4)));
        return rows;
    }
    public async Task<long> SaveAvailabilityAsync(AvailabilityRecord item, bool create, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await LockEmployee(c, t, item.EmployeeId, ct);
        if (!create) await RequireAvailability(c, t, item.EmployeeId, item.Id, ct);
        await using var q = Cmd(c, t, create
            ? "INSERT INTO employee_availability (employee_id,starts_at,ends_at,status) VALUES (@employee,@start,@end,@status)"
            : "UPDATE employee_availability SET starts_at=@start,ends_at=@end,status=@status WHERE id=@id AND employee_id=@employee",
            ("id", item.Id), ("employee", item.EmployeeId), ("start", item.StartsAt), ("end", item.EndsAt), ("status", item.Status));
        await q.ExecuteNonQueryAsync(ct); var id = create ? q.LastInsertedId : item.Id;
        await CheckCoverage(c, t, item.EmployeeId, ct); await t.CommitAsync(ct); return id;
    }
    public async Task DeleteAvailabilityAsync(long employeeId, long id, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await LockEmployee(c, t, employeeId, ct); await RequireAvailability(c, t, employeeId, id, ct);
        await using var q = Cmd(c, t, "DELETE FROM employee_availability WHERE id=@id AND employee_id=@employee", ("id", id), ("employee", employeeId));
        await q.ExecuteNonQueryAsync(ct); await CheckCoverage(c, t, employeeId, ct); await t.CommitAsync(ct);
    }
    private static async Task RequireAvailability(MySqlConnection c, MySqlTransaction t, long employee, long id, CancellationToken ct)
    {
        await using var q = Cmd(c, t, "SELECT id FROM employee_availability WHERE id=@id AND employee_id=@employee", ("id", id), ("employee", employee));
        if (await q.ExecuteScalarAsync(ct) is null) throw new ApiException(404, "Không tìm thấy khoảng lịch.");
    }
    public async Task<IReadOnlyList<SkillRecord>> SkillsAsync(CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var q = c.Sql("SELECT id,name,description FROM skills ORDER BY id");
        await using var r = await q.ExecuteReaderAsync(ct); var rows = new List<SkillRecord>();
        while (await r.ReadAsync(ct)) rows.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2))); return rows;
    }
    public async Task<long> SaveSkillAsync(SkillRecord item, bool create, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        if (!create)
        {
            await using var exists = c.Sql("SELECT id FROM skills WHERE id=@id", ("id", item.Id));
            if (await exists.ExecuteScalarAsync(ct) is null) throw new ApiException(404, "Không tìm thấy kỹ năng.");
        }
        await using var q = c.Sql(create ? "INSERT INTO skills (name,description) VALUES (@name,@description)" : "UPDATE skills SET name=@name,description=@description WHERE id=@id",
            ("id", item.Id), ("name", item.Name), ("description", item.Description));
        await q.ExecuteNonQueryAsync(ct); return create ? q.LastInsertedId : item.Id;
    }
    public async Task<IReadOnlyList<EmployeeSkillRecord>> EmployeeSkillsAsync(long employeeId, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        await using var q = c.Sql("SELECT s.id,s.name,e.level FROM employee_skills e JOIN skills s ON s.id=e.skill_id WHERE e.employee_id=@id ORDER BY s.id", ("id", employeeId));
        await using var r = await q.ExecuteReaderAsync(ct); var rows = new List<EmployeeSkillRecord>();
        while (await r.ReadAsync(ct)) rows.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2))); return rows;
    }
    public async Task SetEmployeeSkillsAsync(long employeeId, IReadOnlyList<(long Id, string Level)> skills, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await LockEmployee(c, t, employeeId, ct); await NoActiveAssignments(c, t, employeeId, ct);
        foreach (var skill in skills)
        {
            await using var exists = Cmd(c, t, "SELECT id FROM skills WHERE id=@id", ("id", skill.Id));
            if (await exists.ExecuteScalarAsync(ct) is null) throw new ApiException(404, "Không tìm thấy kỹ năng.");
        }
        await using (var delete = Cmd(c, t, "DELETE FROM employee_skills WHERE employee_id=@id", ("id", employeeId))) await delete.ExecuteNonQueryAsync(ct);
        foreach (var skill in skills)
        {
            await using var insert = Cmd(c, t, "INSERT INTO employee_skills (employee_id,skill_id,level) VALUES (@employee,@skill,@level)", ("employee", employeeId), ("skill", skill.Id), ("level", skill.Level));
            await insert.ExecuteNonQueryAsync(ct);
        }
        await t.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<AssignmentSnapshot>> AssignmentsAsync(long bookingId, long? userId, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        await using var q = c.Sql(AssignmentSelect + " WHERE (@booking=0 OR a.booking_id=@booking) AND (@user IS NULL OR e.user_id=@user) ORDER BY a.scheduled_start DESC,a.id", ("booking", bookingId), ("user", userId));
        await using var r = await q.ExecuteReaderAsync(ct); var rows = new List<AssignmentSnapshot>();
        while (await r.ReadAsync(ct)) rows.Add(Assignment(r)); return rows;
    }

    public async Task<StaffWorkflowResult> ExecuteAsync(StaffWorkflowCommand command, CancellationToken ct)
    {
        var serialized = JsonSerializer.Serialize(command, Json);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
        await using var c = await connections.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await using (var reserve = Cmd(c, t, "INSERT INTO processed_operations (operation_id,payload_hash,result) VALUES (@id,@hash,'{}') ON DUPLICATE KEY UPDATE operation_id=operation_id", ("id", command.OperationId), ("hash", hash)))
            await reserve.ExecuteNonQueryAsync(ct);
        await using (var old = Cmd(c, t, "SELECT payload_hash,result FROM processed_operations WHERE operation_id=@id FOR UPDATE", ("id", command.OperationId)))
        {
            await using var r = await old.ExecuteReaderAsync(ct); await r.ReadAsync(ct);
            if (r.GetString(0) != hash) throw new ApiException(409, "Mã thao tác đã được dùng với dữ liệu khác.");
            var result = r.GetString(1);
            if (result != "{}")
            {
                using var stored = JsonDocument.Parse(result);
                if (stored.RootElement.TryGetProperty("errorStatus", out var status))
                    throw new ApiException(status.GetInt32(), stored.RootElement.GetProperty("errorMessage").GetString()!);
                return JsonSerializer.Deserialize<StaffWorkflowResult>(result, Json)!;
            }
        }
        await using (var savepoint = Cmd(c, t, "SAVEPOINT workflow_body")) await savepoint.ExecuteNonQueryAsync(ct);
        try
        {
        var dispatch=await LockDispatch(c,t,command.BookingId,ct);
        var employeeIds = new List<long>(command.EmployeeIds);
        long claimEmployee=0;
        if(command.Kind=="claim")
        {
            claimEmployee=await Number(c,t,"SELECT id FROM employees WHERE user_id=@user",ct,("user",command.ActorId));
            if(claimEmployee==0||dispatch is null)throw new ApiException(404,"Không có lời mời nhận việc.");
            employeeIds.Add(claimEmployee);
        }
        await using (var find = Cmd(c, t, "SELECT DISTINCT employee_id FROM assignments WHERE booking_id=@booking ORDER BY employee_id", ("booking", command.BookingId)))
        {
            await using var r = await find.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) employeeIds.Add(r.GetInt64(0));
        }
        var employees = new Dictionary<long, EmployeeRecord>();
        foreach (var id in employeeIds.Distinct().Order()) employees[id] = await LockEmployee(c, t, id, ct);
        var current = new List<AssignmentSnapshot>();
        await using (var find = Cmd(c, t, AssignmentSelect + " WHERE a.booking_id=@booking ORDER BY a.employee_id FOR UPDATE", ("booking", command.BookingId)))
        {
            await using var r = await find.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) current.Add(Assignment(r));
        }
        string? outcome=null;long? withdrawalId=null;
        switch (command.Kind)
        {
            case "withdraw":case "withdraw_approve":case "withdraw_reject":
                (outcome,withdrawalId)=await Withdraw(c,t,command,dispatch,current,ct);
                break;
            case "claim":
                var claimant=employees[claimEmployee];
                if(claimant.Status!="active"||claimant.VerificationStatus!="verified"||claimant.BranchId!=dispatch!.Payload.BranchId)throw new ApiException(409,"Hồ sơ không đủ điều kiện nhận đơn.");
                if(await Number(c,t,"SELECT COUNT(*) FROM branches WHERE id=@id AND status='active'",ct,("id",claimant.BranchId))!=1)throw new ApiException(409,"Chi nhánh đã ngừng hoạt động.");
                await ValidateClaim(c,t,dispatch!,claimEmployee,command.ActorId,command.AssignmentId??0,ct);
                await using(var claim=Cmd(c,t,"INSERT INTO assignments(booking_id,employee_id,assigned_at,status,scheduled_start,scheduled_end,assigned_by) SELECT @booking,@employee,UTC_TIMESTAMP(6),'accepted',@start,@end,@actor FROM dispatch_offers WHERE id=@offer AND state='pending' AND expires_at>UTC_TIMESTAMP(6) AND @deadline>UTC_TIMESTAMP(6) AND @start>UTC_TIMESTAMP(6) ON DUPLICATE KEY UPDATE status='accepted',scheduled_start=@start,scheduled_end=@end,version=version+1",("booking",command.BookingId),("employee",claimEmployee),("start",dispatch!.Payload.Start),("end",dispatch.Payload.End),("actor",command.ActorId),("offer",command.AssignmentId),("deadline",dispatch.SearchUntil)))
                    if(await claim.ExecuteNonQueryAsync(ct)==0)throw new ApiException(409,"Lời mời vừa hết hạn; chưa giữ vị trí nào.");
                await Write(c,t,"INSERT INTO assignment_receipts(assignment_id,accepted_at) SELECT id,UTC_TIMESTAMP(6) FROM assignments WHERE booking_id=@booking AND employee_id=@employee ON DUPLICATE KEY UPDATE assignment_id=assignment_id",ct,("booking",command.BookingId),("employee",claimEmployee));
                await Write(c,t,"INSERT IGNORE INTO replacement_assignments(assignment_id,created_at) SELECT a.id,UTC_TIMESTAMP(6) FROM assignments a WHERE a.booking_id=@booking AND a.employee_id=@employee AND EXISTS(SELECT 1 FROM assignment_exits x JOIN assignments old ON old.id=x.assignment_id WHERE old.booking_id=@booking)",ct,("booking",command.BookingId),("employee",claimEmployee));
                  await Write(c,t,"UPDATE dispatch_offers SET state='accepted',responded_at=UTC_TIMESTAMP(6) WHERE id=@id",ct,("id",command.AssignmentId));
                await Write(c,t,"UPDATE employee_presence SET ready_since=UTC_TIMESTAMP(6) WHERE employee_id=@id",ct,("id",claimEmployee));
                break;
            case "assign":
                var assignmentStart=command.ScheduledStart;var assignmentEnd=command.ScheduledEnd;
                if(command.ScheduledStart<DateTime.UtcNow||current.Any(a=>a.Status is "in_progress" or "done")){
                    if(dispatch is null||await Number(c,t,"SELECT COUNT(*) FROM assignment_exits x JOIN assignments a ON a.id=x.assignment_id WHERE a.booking_id=@id",ct,("id",command.BookingId))==0)throw new ApiException(409,"Cần có vị trí thay thế đã được duyệt cho đơn đang làm.");
                    assignmentStart=new[]{dispatch.Payload.Start,DateTime.UtcNow.AddMinutes(1)}.Max();assignmentEnd=dispatch.Payload.End;
                    if(assignmentStart>=assignmentEnd)throw new ApiException(409,"Không còn khung giờ thực hiện để phân công thay thế.");
                }
                if(dispatch is not null&&current.Count(a=>a.Status!="cancelled")+command.EmployeeIds.Distinct().Count(id=>!current.Any(a=>a.EmployeeId==id&&a.Status!="cancelled"))>dispatch.Payload.RequiredStaff)throw new ApiException(409,"Phân công sẽ vượt số nhân viên khách yêu cầu.");
                if (assignmentStart < DateTime.UtcNow) throw new ApiException(409, "Thời gian phân công phải ở tương lai.");
                foreach (var id in command.EmployeeIds.Distinct().Order())
                {
                    var employee = employees[id];
                    if (employee.Status != "active" || employee.VerificationStatus != "verified" || employee.BranchId != command.BranchId)
                        throw new ApiException(409, "Nhân viên chưa hoạt động, chưa xác minh hoặc khác chi nhánh.");
                    await using (var branch = Cmd(c, t, "SELECT status FROM branches WHERE id=@id FOR UPDATE", ("id", employee.BranchId)))
                        if (Convert.ToString(await branch.ExecuteScalarAsync(ct)) != "active") throw new ApiException(409, "Chi nhánh đang ngừng hoạt động.");
                    foreach (var skill in command.RequiredSkillIds.Concat(dispatch is null?Array.Empty<long>():await ServiceSkills(c,t,dispatch.Payload.ServiceId,ct)).Distinct())
                    {
                        await using var has = Cmd(c, t, "SELECT COUNT(*) FROM employee_skills WHERE employee_id=@employee AND skill_id=@skill", ("employee", id), ("skill", skill));
                        if (Convert.ToInt64(await has.ExecuteScalarAsync(ct)) == 0) throw new ApiException(409, "Nhân viên chưa đáp ứng kỹ năng yêu cầu.");
                    }
                    await RequireSlot(c, t, id, assignmentStart, assignmentEnd, ct);
                    await using (var overlap = Cmd(c, t, "SELECT COUNT(*) FROM assignments WHERE employee_id=@employee AND booking_id<>@booking AND status<>'cancelled' AND scheduled_start<@end AND scheduled_end>@start",
                        ("employee", id), ("booking", command.BookingId), ("start", assignmentStart), ("end", assignmentEnd)))
                        if (Convert.ToInt64(await overlap.ExecuteScalarAsync(ct)) > 0) throw new ApiException(409, "Nhân viên đã có công việc trùng thời gian.");
                    var existing = current.FirstOrDefault(a => a.EmployeeId == id);
                    if (existing is not null && existing.Status is not ("waiting" or "accepted" or "cancelled")) throw new ApiException(409, "Phân công đã bắt đầu.");
                    await using var save = Cmd(c, t, """
                        INSERT INTO assignments (booking_id,employee_id,assigned_at,status,scheduled_start,scheduled_end,assigned_by)
                        VALUES (@booking,@employee,UTC_TIMESTAMP(6),'waiting',@start,@end,@actor)
                        ON DUPLICATE KEY UPDATE status=IF(status='cancelled','waiting',status),scheduled_start=@start,scheduled_end=@end,assigned_by=@actor,version=version+1
                        """, ("booking", command.BookingId), ("employee", id), ("start", assignmentStart), ("end", assignmentEnd), ("actor", command.ActorId));
                    await save.ExecuteNonQueryAsync(ct);
                    await Write(c,t,"INSERT IGNORE INTO replacement_assignments(assignment_id,created_at) SELECT a.id,UTC_TIMESTAMP(6) FROM assignments a WHERE a.booking_id=@booking AND a.employee_id=@employee AND EXISTS(SELECT 1 FROM assignment_exits x JOIN assignments old ON old.id=x.assignment_id WHERE old.booking_id=@booking)",ct,("booking",command.BookingId),("employee",id));
                }
                break;
            case "cancel":case "admin_cancel":case "stop":
                await Write(c,t,"UPDATE dispatch_booking_locks SET stopped=TRUE WHERE booking_id=@id",ct,("id",command.BookingId));
                if (command.Kind!="stop"&&current.Any(a => a.Status is "in_progress" or "done")) throw new ApiException(409, "Công việc đã bắt đầu; không thể hủy.");
                await RecordExits(c,t,command,current.Where(a=>a.Status!="cancelled"),ct);
                await using (var cancel = Cmd(c, t, "UPDATE assignments SET status='cancelled',version=version+1 WHERE booking_id=@booking AND status NOT IN ('cancelled','done')", ("booking", command.BookingId))) await cancel.ExecuteNonQueryAsync(ct);
                break;
            case "unassign":
                var target = current.FirstOrDefault(a => a.Id == command.AssignmentId) ?? throw new ApiException(404, "Không tìm thấy phân công.");
                if (target.Status is "in_progress" or "done") throw new ApiException(409, "Phân công đã bắt đầu.");
                await RecordExits(c,t,command,[target],ct);
                await using (var remove = Cmd(c, t, "UPDATE assignments SET status='cancelled',version=version+1 WHERE id=@id", ("id", target.Id))) await remove.ExecuteNonQueryAsync(ct);
                break;
            case "progress":
                var job = current.FirstOrDefault(a => a.Id == command.AssignmentId && a.UserId == command.ActorId)
                    ?? throw new ApiException(404, "Không tìm thấy công việc thuộc nhân viên.");
                if (employees[job.EmployeeId].Status != "active") throw new ApiException(409, "Hồ sơ nhân viên không hoạt động.");
                var next = job.Status switch { "waiting" => "accepted", "accepted" => "in_progress", "in_progress" => "done", _ => null };
                if (next is null || next != command.NextStatus) throw new ApiException(409, "Không thể bỏ qua bước trong quy trình công việc.");
                if(next=="in_progress"&&dispatch is not null&&current.Count(a=>a.Status is "accepted" or "in_progress" or "done")<dispatch.Payload.RequiredStaff)throw new ApiException(409,"Đơn chưa đủ nhân viên nhận việc; chưa thể bắt đầu.");
                if(next=="accepted")
                {
                    await using var receipt=Cmd(c,t,"INSERT INTO assignment_receipts(assignment_id,accepted_at) VALUES(@id,UTC_TIMESTAMP(6)) ON DUPLICATE KEY UPDATE assignment_id=assignment_id",("id",job.Id));
                    await receipt.ExecuteNonQueryAsync(ct);
                }
                await using (var progress = Cmd(c, t, "UPDATE assignments SET status=@status,check_in_at=IF(@status='in_progress',UTC_TIMESTAMP(6),check_in_at),check_out_at=IF(@status='done',UTC_TIMESTAMP(6),check_out_at),version=version+1 WHERE id=@id", ("status", next), ("id", job.Id))) await progress.ExecuteNonQueryAsync(ct);
                if(next=="done")await Write(c,t,"UPDATE assignment_withdrawals SET state='closed',reviewed_by=@actor,reviewed_at=UTC_TIMESTAMP(6),review_note='Assignment completed before withdrawal approval' WHERE assignment_id=@id AND state='pending'",ct,("id",job.Id),("actor",command.ActorId));
                break;
            default: throw new ApiException(400, "Loại thao tác không hợp lệ.");
        }
        if(command.Kind is not("withdraw" or "withdraw_approve" or "withdraw_reject"))await AfterDispatchWorkflow(c,t,dispatch,command,ct);
        StaffWorkflowResult aggregate;
        await using (var count = Cmd(c, t, "SELECT COUNT(*),COALESCE(SUM(status IN ('in_progress','done')),0),COALESCE(SUM(status='done'),0) FROM assignments WHERE booking_id=@booking AND status<>'cancelled'", ("booking", command.BookingId)))
        {
            await using var r = await count.ExecuteReaderAsync(ct); await r.ReadAsync(ct);
            aggregate = new(Convert.ToInt32(r.GetValue(0)), Convert.ToInt32(r.GetValue(1)), Convert.ToInt32(r.GetValue(2)),dispatch?.Payload.RequiredStaff??0,outcome,withdrawalId);
        }
        await using (var save = Cmd(c, t, "UPDATE processed_operations SET result=@result WHERE operation_id=@id", ("id", command.OperationId), ("result", JsonSerializer.Serialize(aggregate, Json)))) await save.ExecuteNonQueryAsync(ct);
        await t.CommitAsync(ct); return aggregate;
        }
        catch (ApiException error)
        {
            // Lưu cả thất bại nghiệp vụ: cùng operation luôn nhận cùng kết quả, kể cả retry đồng thời.
            await using (var rollback = Cmd(c, t, "ROLLBACK TO SAVEPOINT workflow_body")) await rollback.ExecuteNonQueryAsync(ct);
            await using (var failed = Cmd(c, t, "UPDATE processed_operations SET result=@result WHERE operation_id=@id",
                ("id", command.OperationId), ("result", JsonSerializer.Serialize(new { errorStatus = error.Status, errorMessage = error.Message }, Json))))
                await failed.ExecuteNonQueryAsync(ct);
            await t.CommitAsync(ct);
            throw;
        }
    }

    private static async Task<EmployeeRecord> LockEmployee(MySqlConnection c, MySqlTransaction t, long id, CancellationToken ct)
    {
        await using var q = Cmd(c, t, $"SELECT {EmployeeColumns} FROM employees WHERE id=@id FOR UPDATE", ("id", id));
        await using var r = await q.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Employee(r) : throw new ApiException(404, "Không tìm thấy nhân viên.");
    }
    private static async Task NoActiveAssignments(MySqlConnection c, MySqlTransaction t, long id, CancellationToken ct)
    {
        await using var q = Cmd(c, t, "SELECT COUNT(*) FROM assignments WHERE employee_id=@id AND status IN ('waiting','accepted','in_progress')", ("id", id));
        if (Convert.ToInt64(await q.ExecuteScalarAsync(ct)) > 0) throw new ApiException(409, "Nhân viên đang có phân công; cần xử lý công việc trước khi thay đổi hồ sơ/kỹ năng.");
    }
    private static async Task RequireSlot(MySqlConnection c, MySqlTransaction t, long employee, DateTime start, DateTime end, CancellationToken ct)
    {
        await using var available = Cmd(c, t, "SELECT COUNT(*) FROM employee_availability WHERE employee_id=@employee AND status='available' AND starts_at<=@start AND ends_at>=@end", ("employee", employee), ("start", start), ("end", end));
        await using var blocked = Cmd(c, t, "SELECT COUNT(*) FROM employee_availability WHERE employee_id=@employee AND status='unavailable' AND starts_at<@end AND ends_at>@start", ("employee", employee), ("start", start), ("end", end));
        if (Convert.ToInt64(await available.ExecuteScalarAsync(ct)) == 0 || Convert.ToInt64(await blocked.ExecuteScalarAsync(ct)) > 0)
            throw new ApiException(409, "Nhân viên không có lịch rảnh bao phủ toàn bộ khung giờ.");
    }
    private static async Task CheckCoverage(MySqlConnection c, MySqlTransaction t, long employee, CancellationToken ct)
    {
        var jobs = new List<(DateTime Start, DateTime End)>();
        await using (var q = Cmd(c, t, "SELECT scheduled_start,scheduled_end FROM assignments WHERE employee_id=@id AND status IN ('waiting','accepted','in_progress') AND scheduled_end>UTC_TIMESTAMP(6)", ("id", employee)))
        { await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) jobs.Add((r.GetDateTime(0), r.GetDateTime(1))); }
        foreach (var job in jobs) await RequireSlot(c, t, employee, job.Start, job.End, ct);
    }
    private async Task<(IReadOnlyList<T> Items, long Total)> PageAsync<T>(string select, string countSql, int page, int size,
        Func<MySqlDataReader, T> map, CancellationToken ct, params (string, object?)[] values)
    {
        await using var c = await connections.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var q = Cmd(c, t, select + " ORDER BY id DESC LIMIT @size OFFSET @offset", [.. values, ("size", size), ("offset", (page - 1) * size)]);
        var items = new List<T>(); await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) items.Add(map(r));
        await using var count = Cmd(c, t, countSql, values); var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        await t.CommitAsync(ct); return (items, total);
    }
}
