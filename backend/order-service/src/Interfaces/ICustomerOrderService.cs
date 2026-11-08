using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.DTOs.Responses;

namespace CleanHome.Order.Api.Interfaces;

public interface ICustomerOrderService
{
    Task<OrderPageResponse> ListAsync(long customerId, OrderListQuery query, CancellationToken ct);
    Task<OrderPageResponse> HistoryAsync(long customerId, OrderHistoryQuery query, CancellationToken ct);
    Task<OrderDetailResponse> GetAsync(long customerId, long id, CancellationToken ct);
}
