using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;

namespace CleanHome.Order.Api.Services;

public sealed class CustomerOrderService(ICustomerOrderRepository orders) : ICustomerOrderService
{
    public Task<OrderPageResponse> ListAsync(long customerId, OrderListQuery query, CancellationToken ct) =>
        ListCoreAsync(customerId, query, string.IsNullOrEmpty(query.Status) ? null : query.Status, ct);
    public Task<OrderPageResponse> HistoryAsync(long customerId, OrderHistoryQuery query, CancellationToken ct) =>
        ListCoreAsync(customerId, query, "completed", ct);

    private async Task<OrderPageResponse> ListCoreAsync(long customerId, OrderHistoryQuery query, string? status, CancellationToken ct)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 100 || query.Search?.Length > 150 || query.ServiceId is <= 0)
            throw new ApiException(400, "Tiêu chí tìm kiếm/phân trang không hợp lệ.");
        if (query.From is not null && query.To is not null && query.From >= query.To)
            throw new ApiException(400, "Thời điểm bắt đầu phải nhỏ hơn thời điểm kết thúc.");
        if (status is not (null or "pending_payment" or "pending" or "confirmed" or "in_progress" or "completed" or "cancelled"))
            throw new ApiException(400, "Trạng thái đơn không hợp lệ.");
        var filter = new OrderFilter(query.Page, query.PageSize, string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.ServiceId, query.From?.UtcDateTime, query.To?.UtcDateTime, status);
        var (items, total) = await orders.ListAsync(customerId, filter, ct);
        return new OrderPageResponse(items.Select(Map).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<OrderDetailResponse> GetAsync(long customerId, long id, CancellationToken ct)
    {
        if (id <= 0) throw new ApiException(400, "Mã đơn phải là số nguyên dương.");
        var order = await orders.FindAsync(customerId, id, ct) ?? throw new ApiException(404, "Không tìm thấy đơn dịch vụ.");
        var s = order.Summary;
        return new OrderDetailResponse(s.Id, s.Code, s.ScheduledStart, s.ScheduledEnd, s.Status, s.TotalAmount, s.CreatedAt,
            order.CustomerId, order.BranchId, order.AddressSnapshot, order.SpecialRequest, order.SubtotalAmount, order.DiscountAmount, order.Version,
            order.Items.Select(i => new OrderItemResponse(i.Id, i.ServiceId, i.ServiceName, i.Quantity, i.UnitPrice, i.Unit, i.EntitlementId)).ToList(),
            order.History.Select(h => new OrderHistoryResponse(h.Id, h.Status, h.Note, h.ChangedBy, h.CreatedAt)).ToList());
    }

    private static OrderSummaryResponse Map(OrderSummary order) => new(order.Id, order.Code, order.ScheduledStart, order.ScheduledEnd,
        order.Status, order.TotalAmount, order.CreatedAt);
}
