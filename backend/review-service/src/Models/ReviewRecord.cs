namespace CleanHome.Review.Api.Models;

public sealed record ReviewRecord(long Id, long BookingId, long EmployeeId, long CustomerId, long AssignmentId,
    int Rating, string Comment, DateTime CreatedAt, bool IsHidden, string? HiddenReason, long? ModeratedBy, DateTime? ModeratedAt);
public sealed record ReviewFilter(int Page, int PageSize, long? BookingId, long? EmployeeId, int? Rating,
    bool? IsHidden, DateTime? From, DateTime? To);
