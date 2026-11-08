using CleanHome.Review.Api.Models;

namespace CleanHome.Review.Api.Interfaces;

public interface IReviewRepository
{
    Task<long> CreateAsync(ReviewRecord review, CancellationToken ct);
    Task<ReviewRecord?> FindAsync(long id, long? customerId, CancellationToken ct);
    Task<(IReadOnlyList<ReviewRecord> Items, long Total, decimal Average)> ListAsync(long? customerId, bool publicOnly, ReviewFilter filter, CancellationToken ct);
    Task<bool> SetVisibilityAsync(long id, bool hidden, string? reason, long actorId, CancellationToken ct);
}
public interface IReviewEligibilityClient
{
    Task<long> ValidateAsync(long bookingId, long employeeId, string token, CancellationToken ct);
}
