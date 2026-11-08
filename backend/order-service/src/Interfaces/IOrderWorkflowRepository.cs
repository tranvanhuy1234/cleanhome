using CleanHome.Order.Api.Models;

namespace CleanHome.Order.Api.Interfaces;

public interface IOrderWorkflowRepository
{
    Task<IOrderWorkflowTransaction> BeginAsync(CancellationToken ct);
    Task<WorkflowOperation?> OperationAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<string>> DueAsync(CancellationToken ct);
    Task RetryAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<CancellationRecord>> CancellationsAsync(long bookingId, CancellationToken ct);
}
public interface IOrderWorkflowTransaction : IAsyncDisposable
{
    Task<WorkflowBooking?> LockBookingAsync(long id, CancellationToken ct);
    Task<WorkflowOperation?> OperationAsync(string id, CancellationToken ct);
    Task<WorkflowOperation?> FindKeyAsync(long actorId, string key, CancellationToken ct);
    Task<bool> HasOperationAsync(long bookingId, CancellationToken ct);
    Task<bool> DispatchReadyAsync(long bookingId,CancellationToken ct);
    Task<CancellationRecord?> PendingCancellationAsync(long bookingId, CancellationToken ct);
    Task<long> AddCancellationAsync(long bookingId, long actorId, string reason, CancellationToken ct);
    Task ResolveCancellationAsync(long id, long actorId, bool approve, string? note, CancellationToken ct);
    Task UpdateBookingAsync(long bookingId, string status, long actorId, string note, CancellationToken ct);
    Task ReleaseReservationsAsync(long bookingId, CancellationToken ct);
    Task CompleteReservationsAsync(long bookingId, CancellationToken ct);
    Task InsertOperationAsync(WorkflowOperation operation, CancellationToken ct);
    Task FinishOperationAsync(string id, string state, string? result, int? errorStatus, string? errorMessage, CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
}
