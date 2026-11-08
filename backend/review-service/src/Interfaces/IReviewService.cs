using CleanHome.Review.Api.DTOs.Requests;
using CleanHome.Review.Api.DTOs.Responses;

namespace CleanHome.Review.Api.Interfaces;

public interface IReviewService
{
    Task<ReviewResponse> SubmitAsync(CreateReviewRequest request, long customerId, string token, CancellationToken ct);
    Task<ReviewResponse> GetAsync(long id, long? customerId, CancellationToken ct);
    Task<ReviewPage<ReviewResponse>> ListAsync(ReviewQuery query, long? customerId, bool? hidden, CancellationToken ct);
    Task<ReviewPage<PublicReviewResponse>> PublicAsync(long employeeId, ReviewQuery query, CancellationToken ct);
    Task<ReviewResponse> SetVisibilityAsync(long id, ReviewVisibilityRequest request, long actorId, CancellationToken ct);
}
