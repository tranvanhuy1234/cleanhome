using CleanHome.Auth.Api.Models;

namespace CleanHome.Auth.Api.Interfaces;

public interface IProfileDelivery
{
    Task<bool> DeliverAsync(long accountId, CancellationToken ct);
}

public interface IProfileOutboxRepository
{
    Task<ProfileOutboxItem?> FindPendingAsync(long accountId, CancellationToken ct);
    Task<IReadOnlyList<long>> ListDueAsync(CancellationToken ct);
    Task MarkDeliveredAsync(long accountId, CancellationToken ct);
    Task ScheduleRetryAsync(long accountId, CancellationToken ct);
}
