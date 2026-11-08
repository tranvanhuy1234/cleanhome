using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.Order.Api.Models;

namespace CleanHome.Order.Api.Services;

internal static class OrderMapping
{
    public static OrderDetailResponse Detail(OrderDetail order)
    {
        var s = order.Summary;
        return new(s.Id, s.Code, s.ScheduledStart, s.ScheduledEnd, s.Status, s.TotalAmount, s.CreatedAt,
            order.CustomerId, order.BranchId, order.AddressSnapshot, order.SpecialRequest, order.SubtotalAmount, order.DiscountAmount, order.Version,
            order.Items.Select(i => new OrderItemResponse(i.Id, i.ServiceId, i.ServiceName, i.Quantity, i.UnitPrice, i.Unit, i.EntitlementId)).ToList(),
            order.History.Select(h => new OrderHistoryResponse(h.Id, h.Status, h.Note, h.ChangedBy, h.CreatedAt)).ToList());
    }
}
