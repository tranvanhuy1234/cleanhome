using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using CleanHome.Staff.Api.DTOs.Requests;
using CleanHome.Staff.Api.DTOs.Responses;
using CleanHome.Staff.Api.Interfaces;
using CleanHome.Staff.Api.Models;

namespace CleanHome.Staff.Api.Services;

public sealed class StaffService(IStaffRepository repository, IHttpClientFactory clients, IConfiguration configuration) : IStaffService
{
    public async Task<StaffPage<BranchResponse>> BranchesAsync(StaffPageQuery query, CancellationToken ct)
    { Page(query); var (rows, total) = await repository.BranchesAsync(query.Page, query.PageSize, ct); return new(rows.Select(Map).ToList(), query.Page, query.PageSize, total); }
    public async Task<BranchResponse> BranchAsync(long id, CancellationToken ct)
    { Id(id); return Map(await repository.BranchAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy chi nhánh.")); }
    public async Task<BranchResponse> SaveBranchAsync(long? id, BranchRequest input, CancellationToken ct)
    {
        if (id is not null) Id(id.Value);
        if (input.ServiceArea.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)) throw new ApiException(400, "Vùng phục vụ phải là JSON object hoặc array.");
        var record = new BranchRecord(id ?? 0, Name(input.Name), input.AddressText.Trim(), input.ServiceArea,
            input.Latitude!.Value, input.Longitude!.Value, input.Status);
        if (record.AddressText.Length < 5) throw new ApiException(400, "Địa chỉ chưa hợp lệ.");
        var result = await repository.SaveBranchAsync(record, id is null, ct); return await BranchAsync(result, ct);
    }
    public async Task<StaffPage<EmployeeResponse>> EmployeesAsync(StaffPageQuery query, CancellationToken ct)
    { Page(query); var (rows, total) = await repository.EmployeesAsync(query.Page, query.PageSize, query.BranchId, ct); return new(rows.Select(Map).ToList(), query.Page, query.PageSize, total); }
    public async Task<EmployeeResponse> EmployeeAsync(long id, CancellationToken ct)
    { Id(id); return Map(await repository.EmployeeAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy nhân viên.")); }
    public async Task<EmployeeResponse> MyEmployeeAsync(long userId, CancellationToken ct) =>
        Map(await repository.EmployeeByUserAsync(userId, ct) ?? throw new ApiException(404, "Chưa có hồ sơ nhân viên."));

    public async Task<EmployeeResponse> CreateEmployeeAsync(EmployeeCreateRequest input, string token, CancellationToken ct)
    {
        await RequireStaffAccount(input.UserId!.Value, token, ct);
        await BranchAsync(input.BranchId!.Value, ct);
        var id = await repository.SaveEmployeeAsync(new(0, input.UserId.Value, input.BranchId.Value, input.VerificationStatus, input.ExperienceYears, 0, input.Status), true, ct);
        return await EmployeeAsync(id, ct);
    }
    public async Task<EmployeeResponse> UpdateEmployeeAsync(long id, EmployeeUpdateRequest input, CancellationToken ct)
    {
        var old = await EmployeeAsync(id, ct); await BranchAsync(input.BranchId!.Value, ct);
        await repository.SaveEmployeeAsync(new(id, old.UserId, input.BranchId.Value, input.VerificationStatus, input.ExperienceYears, old.ResponseRate, input.Status), false, ct);
        return await EmployeeAsync(id, ct);
    }
    public async Task<IReadOnlyList<AvailabilityResponse>> AvailabilityAsync(long id, CancellationToken ct)
    { await EmployeeAsync(id, ct); return (await repository.AvailabilityAsync(id, ct)).Select(Map).ToList(); }
    public async Task<AvailabilityResponse> SaveAvailabilityAsync(long employeeId, long? id, AvailabilityRequest input, CancellationToken ct)
    {
        Id(employeeId); if (id is not null) Id(id.Value);
        var start = input.StartsAt!.Value.UtcDateTime; var end = input.EndsAt!.Value.UtcDateTime;
        if (end <= start || start < DateTime.UtcNow) throw new ApiException(400, "Khoảng lịch phải ở tương lai và kết thúc sau bắt đầu.");
        var saved = await repository.SaveAvailabilityAsync(new(id ?? 0, employeeId, start, end, input.Status), id is null, ct);
        return (await AvailabilityAsync(employeeId, ct)).Single(row => row.Id == saved);
    }
    public Task DeleteAvailabilityAsync(long employeeId, long id, CancellationToken ct)
    { Id(employeeId); Id(id); return repository.DeleteAvailabilityAsync(employeeId, id, ct); }
    public async Task<IReadOnlyList<SkillResponse>> SkillsAsync(CancellationToken ct) => (await repository.SkillsAsync(ct)).Select(Map).ToList();
    public async Task<SkillResponse> SaveSkillAsync(long? id, SkillRequest input, CancellationToken ct)
    {
        if (id is not null) Id(id.Value);
        var saved = await repository.SaveSkillAsync(new(id ?? 0, Name(input.Name), input.Description.Trim()), id is null, ct);
        return (await SkillsAsync(ct)).Single(row => row.Id == saved);
    }
    public async Task<IReadOnlyList<EmployeeSkillResponse>> EmployeeSkillsAsync(long employeeId, CancellationToken ct)
    { await EmployeeAsync(employeeId, ct); return (await repository.EmployeeSkillsAsync(employeeId, ct)).Select(row => new EmployeeSkillResponse(row.SkillId, row.Name, row.Level)).ToList(); }
    public Task SetSkillsAsync(long employeeId, EmployeeSkillsRequest input, CancellationToken ct)
    {
        Id(employeeId);
        if (input.Skills.Select(s => s.SkillId).Distinct().Count() != input.Skills.Length || input.Skills.Any(s => string.IsNullOrWhiteSpace(s.Level)))
            throw new ApiException(400, "Kỹ năng không được trùng hoặc có cấp độ trống.");
        return repository.SetEmployeeSkillsAsync(employeeId, input.Skills.Select(s => (s.SkillId, s.Level.Trim())).ToList(), ct);
    }
    public Task<IReadOnlyList<AssignmentSnapshot>> AssignmentsAsync(long bookingId, long? userId, CancellationToken ct)
    {
        if (bookingId < 0 || (bookingId == 0 && userId is null)) throw new ApiException(400, "Mã đơn phải là số nguyên dương.");
        return repository.AssignmentsAsync(bookingId, userId, ct);
    }
    public Task<StaffWorkflowResult> ExecuteAsync(StaffWorkflowCommand command, CancellationToken ct)
    {
        if (!Guid.TryParseExact(command.OperationId, "N", out _) || command.BookingId <= 0 || command.ActorId <= 0 ||
            command.ScheduledEnd <= command.ScheduledStart || command.EmployeeIds.Length > 20 || command.EmployeeIds.Any(id => id <= 0) ||
            command.RequiredSkillIds.Length > 50 || command.RequiredSkillIds.Any(id => id <= 0)) throw new ApiException(400, "Thao tác nội bộ không hợp lệ.");
        if (command.Kind == "assign" && command.EmployeeIds.Length == 0)
            throw new ApiException(400, "Cần ít nhất một nhân viên.");
        return repository.ExecuteAsync(command, ct);
    }
    private async Task RequireStaffAccount(long id, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, (configuration["Services:AuthUrl"] ?? "http://localhost:5001").TrimEnd('/') + "/accounts/" + id);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await clients.CreateClient("staff-account").SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ApiException(404, "Tài khoản không tồn tại.");
            if (!response.IsSuccessStatusCode) throw new ApiException(503, "Chưa kiểm tra được tài khoản nhân viên.");
            var account = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (account.GetProperty("role").GetString() != "staff" || account.GetProperty("status").GetString() != "active")
                throw new ApiException(409, "Cần tài khoản nhân viên đang hoạt động.");
        }
        catch (HttpRequestException) { throw new ApiException(503, "Dịch vụ tài khoản chưa sẵn sàng."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(503, "Kiểm tra tài khoản quá thời gian."); }
    }
    private static void Id(long id) { if (id <= 0) throw new ApiException(400, "Mã phải là số nguyên dương."); }
    private static void Page(StaffPageQuery q) { if (q.Page is < 1 or > 100000 || q.PageSize is < 1 or > 100 || q.BranchId is <= 0) throw new ApiException(400, "Phân trang không hợp lệ."); }
    private static string Name(string value) { var name = value.Trim(); if (name.Length < 2) throw new ApiException(400, "Tên phải có ít nhất hai ký tự."); return name; }
    private static BranchResponse Map(BranchRecord r) => new(r.Id, r.Name, r.AddressText, r.ServiceArea, r.Latitude, r.Longitude, r.Status);
    private static EmployeeResponse Map(EmployeeRecord r) => new(r.Id, r.UserId, r.BranchId, r.VerificationStatus, r.ExperienceYears, r.ResponseRate, r.Status);
    private static AvailabilityResponse Map(AvailabilityRecord r) => new(r.Id, r.EmployeeId, r.StartsAt, r.EndsAt, r.Status);
    private static SkillResponse Map(SkillRecord r) => new(r.Id, r.Name, r.Description);
}
