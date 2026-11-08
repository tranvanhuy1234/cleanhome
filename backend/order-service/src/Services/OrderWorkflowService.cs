using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Order.Api.Services;

public sealed class OrderWorkflowService(ICustomerOrderRepository orders, IOrderWorkflowRepository workflows,
    IOrderOperationRunner runner, IStaffWorkflowClient staff) : IOrderWorkflowService
{
    public async Task<OrderPageResponse> AdminListAsync(AdminOrderQuery q, CancellationToken ct)
    {
        if (q.Page is < 1 or > 100000 || q.PageSize is < 1 or > 100 || q.Search?.Length > 150 || q.CustomerId is <= 0 || q.ServiceId is <= 0 ||
            (q.From is not null && q.To is not null && q.From >= q.To)) throw new ApiException(400, "Tiêu chí lọc đơn không hợp lệ.");
        var filter = new OrderFilter(q.Page, q.PageSize, string.IsNullOrWhiteSpace(q.Search) ? null : q.Search.Trim(), q.ServiceId,
            q.From?.UtcDateTime, q.To?.UtcDateTime, string.IsNullOrEmpty(q.Status) ? null : q.Status,q.Trash,true);
        var (rows, total) = await orders.ListAsync(q.CustomerId, filter, ct);
        return new(rows.Select(r => new OrderSummaryResponse(r.Id, r.Code, r.ScheduledStart, r.ScheduledEnd, r.Status, r.TotalAmount, r.CreatedAt)).ToList(), q.Page, q.PageSize, total);
    }
    public async Task<OrderDetailResponse> AdminDetailAsync(long id, CancellationToken ct)
    { Id(id); return OrderMapping.Detail(await orders.FindAsync(null, id, ct) ?? throw new ApiException(404, "Không tìm thấy đơn.")); }
    public async Task<OrderDetailResponse> ConfirmAsync(long id, long actorId, OrderNoteRequest request, CancellationToken ct)
    {
        Id(id); await using var transaction = await workflows.BeginAsync(ct);
        var booking = await Booking(transaction, id, ct);
        await Available(transaction, booking, request.ExpectedVersion, ct);
        if (booking.Status != "pending") throw new ApiException(409, "Chỉ xác nhận được đơn đang chờ.");
        if (await transaction.PendingCancellationAsync(id, ct) is not null) throw new ApiException(409, "Đơn đang có yêu cầu hủy.");
        await transaction.UpdateBookingAsync(id, "confirmed", actorId, "Quản trị viên xác nhận đơn. " + Note(request.Note), ct);
        await transaction.CommitAsync(ct); return await AdminDetailAsync(id, ct);
    }
    public async Task<CancellationResponse> RequestCancellationAsync(long id, long customerId, CancellationRequest request, CancellationToken ct)
    {
        Id(id); var reason = request.Reason.Trim(); if (reason.Length < 3) throw new ApiException(400, "Lý do hủy cần ít nhất ba ký tự.");
        long requestId;
        await using (var transaction = await workflows.BeginAsync(ct))
        {
            var booking = await Booking(transaction, id, ct);
            if (booking.CustomerId != customerId) throw new ApiException(404, "Không tìm thấy đơn dịch vụ.");
            await Available(transaction, booking, request.ExpectedVersion, ct);
            if (booking.Status is not ("pending_payment" or "pending" or "confirmed" or "in_progress")) throw new ApiException(409, "Đơn đã bắt đầu, hoàn thành hoặc đã hủy.");
            if (await transaction.PendingCancellationAsync(id, ct) is not null) throw new ApiException(409, "Đã có yêu cầu hủy đang chờ.");
            requestId = await transaction.AddCancellationAsync(id, customerId, reason, ct);
            await transaction.UpdateBookingAsync(id, booking.Status, customerId, "Khách hàng yêu cầu hủy: " + reason, ct);
            await transaction.CommitAsync(ct);
        }
        return Map((await workflows.CancellationsAsync(id, ct)).Single(r => r.Id == requestId));
    }
    public async Task<IReadOnlyList<CancellationResponse>> CancellationsAsync(long id, long? owner, CancellationToken ct)
    {
        Id(id); if (await orders.FindAsync(owner, id, ct) is null) throw new ApiException(404, "Không tìm thấy đơn dịch vụ.");
        return (await workflows.CancellationsAsync(id, ct)).Select(Map).ToList();
    }
    public async Task<CancellationResponse> RejectCancellationAsync(long id, long requestId, long actorId, OrderNoteRequest request, CancellationToken ct)
    {
        Id(id); Id(requestId);
        await using (var transaction = await workflows.BeginAsync(ct))
        {
            var booking = await Booking(transaction, id, ct); await Available(transaction, booking, request.ExpectedVersion, ct);
            var pending = await transaction.PendingCancellationAsync(id, ct);
            if (pending?.Id != requestId) throw new ApiException(409, "Yêu cầu hủy không còn chờ xử lý.");
            await transaction.ResolveCancellationAsync(requestId, actorId, false, Note(request.Note), ct);
            await transaction.UpdateBookingAsync(id, booking.Status, actorId, "Từ chối yêu cầu hủy. " + Note(request.Note), ct);
            await transaction.CommitAsync(ct);
        }
        return Map((await workflows.CancellationsAsync(id, ct)).Single(r => r.Id == requestId));
    }

    public Task<OperationResponse> AssignAsync(long id, long actorId, AssignmentRequest request, CancellationToken ct)
    {
        if (request.EmployeeIds.Any(id => id <= 0) || request.RequiredSkillIds.Any(id => id <= 0) ||
            request.EmployeeIds.Distinct().Count() != request.EmployeeIds.Length || request.RequiredSkillIds.Distinct().Count() != request.RequiredSkillIds.Length)
            throw new ApiException(400, "Mã nhân viên/kỹ năng phải dương và không trùng.");
        return Queue(id, actorId, "assign", request, request.EmployeeIds.Order().ToArray(), request.RequiredSkillIds.Order().ToArray(), null, null, null, null, ct);
    }
    public Task<OperationResponse> UnassignAsync(long id, long actorId, UnassignRequest request, CancellationToken ct) =>
        Queue(id, actorId, "unassign", request, [], [], request.AssignmentId, null, null, null, ct);
    public Task<OperationResponse> ApproveCancellationAsync(long id, long requestId, long actorId, ApproveCancellationRequest request, CancellationToken ct)
    { Id(requestId); return Queue(id, actorId, "cancel", request, [], [], null, null, requestId, Note(request.Note), ct); }
    public Task<OperationResponse> ProgressAsync(long id, long actorId, JobProgressRequest request, CancellationToken ct) =>
        Queue(id, actorId, "progress", request, [], [], request.AssignmentId, request.NextStatus, null, null, ct);
    public async Task<OperationResponse> ClaimAsync(long id,long offerId,long actorId,WorkflowRequest request,CancellationToken ct)
    {
        for(var attempt=0;;attempt++)
        {
            try{return await Queue(id,actorId,"claim",request,[],[],offerId,null,null,null,ct);}
            catch(ApiException error)when(attempt<10&&error.Status==409&&error.Message.StartsWith("Đơn đang xử lý một thao tác.",StringComparison.Ordinal))
            {await Task.Delay(150,ct);}
        }
    }

    public Task<OperationResponse> LifecycleAsync(long id,long actorId,string kind,LifecycleRequest request,long? requestId,CancellationToken ct){
        if(kind is not ("withdraw" or "withdraw_approve" or "withdraw_reject" or "admin_cancel" or "stop")||request.Reason.Trim().Length<3)throw new ApiException(400,"Yêu cầu thay đổi đơn không hợp lệ.");
        return Queue(id,actorId,kind,request,[],[],request.AssignmentId,null,null,request.Reason.Trim(),ct,new WorkflowContext(request.Reason.Trim(),requestId,kind is "admin_cancel" or "stop"?"admin":"employee"));
    }
    private async Task<OperationResponse> Queue(long id, long actorId, string kind, WorkflowRequest request, long[] employees,
        long[] skills, long? assignmentId, string? nextStatus, long? cancellationId, string? note, CancellationToken ct,WorkflowContext? context=null)
    {
        Id(id);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { id, kind, employees, skills, assignmentId, nextStatus, cancellationId, note, request.ExpectedVersion }))));
        if(context is not null)requestHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{requestHash,context}))));
        string operationId;
        await using (var transaction = await workflows.BeginAsync(ct))
        {
            var old = await transaction.FindKeyAsync(actorId, request.IdempotencyKey, ct);
            if (old is not null)
            {
                if (old.BookingId != id || old.Kind != kind || old.Payload.RequestHash != requestHash)
                    throw new ApiException(409, "IdempotencyKey đã dùng với yêu cầu khác.");
                return Map(old);
            }
            if (kind is "progress" or "withdraw")
            {
                var assigned = await staff.AssignmentsAsync(id, ct);
                if (!assigned.Any(a => a.Id == assignmentId && a.UserId == actorId && a.Status != "cancelled"))
                    throw new ApiException(404, "Không tìm thấy công việc thuộc nhân viên.");
            }
            var booking = await Booking(transaction, id, ct);
            await Available(transaction, booking, request.ExpectedVersion, ct);
            var pending = await transaction.PendingCancellationAsync(id, ct);
            if(kind is "admin_cancel" or "stop"){
                if(kind=="stop"?booking.Status!="in_progress":booking.Status is not("pending_payment" or "pending" or "confirmed"))throw new ApiException(409,"Đơn đã bắt đầu. Hãy dùng quy trình dừng công việc và đối soát.");
            }
            else if (kind == "cancel")
            {
                if (pending?.Id != cancellationId) throw new ApiException(409, "Yêu cầu hủy không còn chờ xử lý.");
                if (booking.Status is not ("pending_payment" or "pending" or "confirmed")) throw new ApiException(409, "Đơn không còn đủ điều kiện hủy.");
            }
            else
            {
                if (pending is not null) throw new ApiException(409, "Cần xử lý yêu cầu hủy trước khi thay đổi công việc.");
                if(kind is "withdraw" or "withdraw_approve" or "withdraw_reject"){if(booking.Status is not("pending" or "confirmed" or "in_progress"))throw new ApiException(409,"Đơn không còn hiệu lực để hủy nhận việc.");}
                else if (kind is "assign" or "unassign")
                {
                    if(kind=="assign"&&!await transaction.DispatchReadyAsync(id,ct))throw new ApiException(409,"Đang đồng bộ số vị trí của đơn. Vui lòng thử lại sau.");
                    if ((kind=="assign"&&booking.Status is not("confirmed" or "in_progress"))||(kind=="unassign"&&booking.Status!="confirmed")) throw new ApiException(409, "Chỉ thay đổi phân công của đơn đã xác nhận, chưa bắt đầu.");
                    if (kind == "assign" && booking.Status!="in_progress"&&booking.ScheduledStart < DateTime.UtcNow) throw new ApiException(409, "Lịch phân công phải ở tương lai.");
                }
                else if(kind=="claim"){if(booking.Status is not("pending" or "confirmed" or "in_progress"))throw new ApiException(409,"Đơn không còn tuyển nhân viên.");}
                else if (booking.Status is not ("confirmed" or "in_progress")) throw new ApiException(409, "Đơn không ở trạng thái thực hiện công việc.");
            }
            operationId = Guid.NewGuid().ToString("N"); var now = DateTime.UtcNow;
            var command = new StaffWorkflowCommand(operationId, id, kind, actorId, booking.BranchId, booking.ScheduledStart, booking.ScheduledEnd, employees, skills, assignmentId, nextStatus,context??(kind=="cancel"?new WorkflowContext(pending!.Reason,null,"customer"):null));
            await transaction.InsertOperationAsync(new(operationId, id, actorId, request.IdempotencyKey, kind,
                new(command, requestHash, cancellationId, note), "pending", null, null, null, 0, now, now), ct);
            await transaction.CommitAsync(ct);
        }
        await runner.ExecuteAsync(operationId, ct);
        return Map(await workflows.OperationAsync(operationId, ct) ?? throw new ApiException(503, "Chưa đọc được kết quả thao tác."));
    }
    public async Task<OperationResponse> OperationAsync(string id, long actorId, bool admin, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ApiException(400, "Mã thao tác không hợp lệ.");
        var operation = await workflows.OperationAsync(id, ct);
        if (operation is null || (!admin && operation.ActorId != actorId)) throw new ApiException(404, "Không tìm thấy thao tác.");
        return Map(operation);
    }
    public async Task<IReadOnlyList<AssignmentSnapshot>> CustomerAssignmentsAsync(long id, long customerId, CancellationToken ct)
    {
        Id(id); if (await orders.FindAsync(customerId, id, ct) is null) throw new ApiException(404, "Không tìm thấy đơn dịch vụ.");
        return await staff.AssignmentsAsync(id, ct);
    }
    private static async Task<WorkflowBooking> Booking(IOrderWorkflowTransaction transaction, long id, CancellationToken ct) =>
        await transaction.LockBookingAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy đơn dịch vụ.");
    private static async Task Available(IOrderWorkflowTransaction transaction, WorkflowBooking booking, long? expected, CancellationToken ct)
    {
        if (expected is not null && expected != booking.Version) throw new ApiException(409, "Đơn đã thay đổi. Vui lòng tải lại.");
        if (await transaction.HasOperationAsync(booking.Id, ct)) throw new ApiException(409, "Đơn đang xử lý một thao tác. Vui lòng theo dõi kết quả trước khi tiếp tục.");
    }
    private static void Id(long id) { if (id <= 0) throw new ApiException(400, "Mã phải là số nguyên dương."); }
    private static string? Note(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    private static CancellationResponse Map(CancellationRecord r) => new(r.Id, r.BookingId, r.RequestedBy, r.Reason, r.Status, r.RequestedAt, r.ResolvedBy, r.ResolvedAt, r.AdminNote);
    private static OperationResponse Map(WorkflowOperation r) => new(r.Id, r.BookingId, r.Kind, r.State, r.Result, r.ErrorStatus, r.ErrorMessage, r.Attempts, r.CreatedAt, r.UpdatedAt);
}
