using System.Text.Json;

namespace CleanHome.Order.Api.DTOs.Responses;

public sealed record OrderSummaryResponse(long Id, string Code, DateTime ScheduledStart, DateTime ScheduledEnd, string Status, decimal TotalAmount, DateTime CreatedAt);
public sealed record OrderPageResponse(IReadOnlyList<OrderSummaryResponse> Items, int Page, int PageSize, long Total);
public sealed record OrderItemResponse(long Id, long ServiceId, string ServiceName, decimal Quantity, decimal UnitPrice, string Unit, long? EntitlementId);
public sealed record OrderHistoryResponse(long Id, string Status, string Note, long ChangedBy, DateTime CreatedAt);
public sealed record OrderDetailResponse(long Id, string Code, DateTime ScheduledStart, DateTime ScheduledEnd, string Status,
    decimal TotalAmount, DateTime CreatedAt, long CustomerId, long BranchId, JsonElement AddressSnapshot,
    string? SpecialRequest, decimal SubtotalAmount, decimal DiscountAmount, long Version,
    IReadOnlyList<OrderItemResponse> Items, IReadOnlyList<OrderHistoryResponse> History);
