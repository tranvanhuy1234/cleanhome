namespace CleanHome.Review.Api.DTOs.Responses;

public sealed record ReviewResponse(long Id, long BookingId, long EmployeeId, long CustomerId, long AssignmentId,
    int Rating, string Comment, DateTime CreatedAt, bool IsHidden, string? HiddenReason, long? ModeratedBy, DateTime? ModeratedAt);
public sealed record PublicReviewResponse(long Id, long EmployeeId, int Rating, string Comment, DateTime CreatedAt);
public sealed record ReviewPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total, decimal AverageRating);
