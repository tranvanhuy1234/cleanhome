using System.Text.Json;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Order.Api.Services;

public sealed class OrderOperationRunner(IOrderWorkflowRepository repository, IStaffWorkflowClient staff, ILogger<OrderOperationRunner> logger) : IOrderOperationRunner
{
    public async Task ExecuteAsync(string id, CancellationToken ct)
    {
        try
        {
            var operation = await repository.OperationAsync(id, ct);
            if (operation is null || operation.State != "pending") return;
            StaffWorkflowResult result;
            try { result = await staff.ExecuteAsync(operation.Payload.Command, ct); }
            catch (ApiException error) when (error.Status is 400 or 404 or 409)
            {
                await using var failed = await repository.BeginAsync(ct);
                await failed.LockBookingAsync(operation.BookingId, ct);
                if ((await failed.OperationAsync(id, ct))?.State == "pending")
                    await failed.FinishOperationAsync(id, "failed", null, error.Status, error.Message, ct);
                await failed.CommitAsync(ct); return;
            }
            await using var transaction = await repository.BeginAsync(ct);
            var booking = await transaction.LockBookingAsync(operation.BookingId, ct) ?? throw new ApiException(404, "Không tìm thấy đơn.");
            var current = await transaction.OperationAsync(id, ct);
            if (current?.State != "pending") return;
            var status = booking.Status;
            var note = operation.Kind switch
            {
                "assign" => "Quản trị viên phân công nhân viên", "unassign" => "Quản trị viên gỡ phân công",
                "cancel" => "Quản trị viên duyệt hủy đơn", "claim"=>"Nhân viên tự nhận lời mời", "progress" => "Nhân viên cập nhật công việc: " + operation.Payload.Command.NextStatus,
                _ => "Cập nhật công việc"
            };
            if (operation.Kind is "cancel" or "admin_cancel" or "stop")
            {
                var cancellation = await transaction.PendingCancellationAsync(booking.Id, ct);
                if (operation.Kind=="cancel"&&(cancellation is null || cancellation.Id != operation.Payload.CancellationId)) throw new InvalidOperationException("Yêu cầu hủy không khớp operation.");
                if(cancellation is not null)await transaction.ResolveCancellationAsync(cancellation.Id, operation.ActorId, true, operation.Payload.Note, ct);
                status = "cancelled"; await transaction.ReleaseReservationsAsync(booking.Id, ct);
            }
            else if(operation.Kind=="claim"&&booking.Status!="in_progress")status="confirmed";
            else if(result.Outcome=="withdrawn"&&booking.Status!="in_progress"&&result.ActiveCount<result.RequiredCount)status="pending";
            if(operation.Kind.StartsWith("withdraw",StringComparison.Ordinal))note=result.Outcome switch {"requested"=>"Nhân viên yêu cầu hủy nhận việc, chờ admin duyệt.","withdrawn"=>"Đã gỡ phần việc của một nhân viên; đang tìm người thay thế.","rejected"=>"Admin từ chối yêu cầu hủy nhận việc.",_=>"Cập nhật yêu cầu nhân sự."};
            else if (operation.Kind == "progress")
            {
                if (result.ActiveCount > 0 && result.ActiveCount>=result.RequiredCount && result.DoneCount == result.ActiveCount)
                { status = "completed"; await transaction.CompleteReservationsAsync(booking.Id, ct); }
                else if (result.StartedCount > 0) status = "in_progress";
            }
            if(operation.Kind is "admin_cancel" or "stop")note=(operation.Kind=="stop"?"Admin dừng công việc và chuyển đối soát: ":"Admin hủy đơn: ")+operation.Payload.Note;
            if(result.Outcome!="unchanged")await transaction.UpdateBookingAsync(booking.Id, status, operation.ActorId, note, ct);
            await transaction.FinishOperationAsync(id, "succeeded", JsonSerializer.Serialize(new { bookingStatus = status, version = booking.Version + (result.Outcome=="unchanged"?0:1),
                activeAssignments = result.ActiveCount, doneAssignments = result.DoneCount, withdrawalOutcome=result.Outcome,withdrawalRequestId=result.RequestId,refundAssessmentPending = operation.Kind is "cancel" or "admin_cancel" or "stop" }), null, null, ct);
            await transaction.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            logger.LogWarning("Operation {OperationId} chưa hoàn tất; sẽ thử lại từ dữ liệu đã lưu.", id);
            try { await repository.RetryAsync(id, ct); } catch (Exception) { logger.LogWarning("Chưa ghi được lịch retry operation {OperationId}.", id); }
        }
    }
}

public sealed class OrderWorkflowWorker(IServiceScopeFactory scopes, ILogger<OrderWorkflowWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOrderWorkflowRepository>();
                var runner = scope.ServiceProvider.GetRequiredService<IOrderOperationRunner>();
                foreach (var id in await repository.DueAsync(stoppingToken)) await runner.ExecuteAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Hàng đợi thao tác đơn chưa sẵn sàng; sẽ thử lại."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
