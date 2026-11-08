using CleanHome.Review.Api.DTOs.Requests;
using CleanHome.Review.Api.DTOs.Responses;
using CleanHome.Review.Api.Interfaces;
using CleanHome.Review.Api.Models;
using CleanHome.ServiceDefaults;

namespace CleanHome.Review.Api.Services;

public sealed class ReviewService(IReviewRepository reviews, IReviewEligibilityClient eligibility) : IReviewService
{
    public async Task<ReviewResponse> SubmitAsync(CreateReviewRequest request, long customerId, string token, CancellationToken ct)
    {
        var comment = request.Comment.Trim(); if (comment.Length == 0) throw new ApiException(400, "Nội dung đánh giá không được trống.");
        var assignment = await eligibility.ValidateAsync(request.BookingId, request.EmployeeId, token, ct);
        var id = await reviews.CreateAsync(new(0, request.BookingId, request.EmployeeId, customerId, assignment, request.Rating, comment, DateTime.UtcNow, false, null, null, null), ct);
        return await GetAsync(id, customerId, ct);
    }
    public async Task<ReviewResponse> GetAsync(long id, long? customerId, CancellationToken ct)
    { Id(id); return Map(await reviews.FindAsync(id, customerId, ct) ?? throw new ApiException(404, "Không tìm thấy đánh giá.")); }
    public async Task<ReviewPage<ReviewResponse>> ListAsync(ReviewQuery query, long? customerId, bool? hidden, CancellationToken ct)
    {
        var (items, total, average) = await reviews.ListAsync(customerId, false, Filter(query, hidden), ct);
        return new(items.Select(Map).ToList(), query.Page, query.PageSize, total, decimal.Round(average, 2));
    }
    public async Task<ReviewPage<PublicReviewResponse>> PublicAsync(long employeeId, ReviewQuery query, CancellationToken ct)
    {
        Id(employeeId); var filter = Filter(query, false) with { EmployeeId = employeeId, BookingId = null };
        var (items, total, average) = await reviews.ListAsync(null, true, filter, ct);
        return new(items.Select(r => new PublicReviewResponse(r.Id, r.EmployeeId, r.Rating, r.Comment, r.CreatedAt)).ToList(), query.Page, query.PageSize, total, decimal.Round(average, 2));
    }
    public async Task<ReviewResponse> SetVisibilityAsync(long id, ReviewVisibilityRequest request, long actorId, CancellationToken ct)
    {
        Id(id); var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (request.IsHidden == true && reason is null) throw new ApiException(400, "Cần nhập lý do ẩn đánh giá.");
        if (!await reviews.SetVisibilityAsync(id, request.IsHidden!.Value, request.IsHidden == true ? reason : null, actorId, ct))
            throw new ApiException(404, "Không tìm thấy đánh giá.");
        return await GetAsync(id, null, ct);
    }
    private static ReviewFilter Filter(ReviewQuery q, bool? hidden)
    {
        if (q.Page is < 1 or > 100000 || q.PageSize is < 1 or > 100 || q.BookingId is <= 0 || q.EmployeeId is <= 0 || q.Rating is < 1 or > 5 ||
            (q.From is not null && q.To is not null && q.From >= q.To)) throw new ApiException(400, "Tiêu chí lọc đánh giá không hợp lệ.");
        return new(q.Page, q.PageSize, q.BookingId, q.EmployeeId, q.Rating, hidden, q.From?.UtcDateTime, q.To?.UtcDateTime);
    }
    private static void Id(long id) { if (id <= 0) throw new ApiException(400, "Mã phải là số nguyên dương."); }
    private static ReviewResponse Map(ReviewRecord r) => new(r.Id, r.BookingId, r.EmployeeId, r.CustomerId, r.AssignmentId, r.Rating, r.Comment, r.CreatedAt, r.IsHidden, r.HiddenReason, r.ModeratedBy, r.ModeratedAt);
}
