using System.Text.Json;

namespace CleanHome.Order.Api.Models;

public sealed record OrderFilter(int Page, int PageSize, string? Search, long? ServiceId, DateTime? From, DateTime? To, string? Status,bool Trash=false,bool AdminOnly=false);
public sealed record OrderSummary(long Id, string Code, DateTime ScheduledStart, DateTime ScheduledEnd, string Status, decimal TotalAmount, DateTime CreatedAt);
public sealed record OrderItem(long Id, long ServiceId, string ServiceName, decimal Quantity, decimal UnitPrice, string Unit, long? EntitlementId);
public sealed record OrderHistory(long Id, string Status, string Note, long ChangedBy, DateTime CreatedAt);
public sealed record OrderDetail(OrderSummary Summary, long CustomerId, long BranchId, JsonElement AddressSnapshot,
    string? SpecialRequest, decimal SubtotalAmount, decimal DiscountAmount, long Version, IReadOnlyList<OrderItem> Items, IReadOnlyList<OrderHistory> History);
