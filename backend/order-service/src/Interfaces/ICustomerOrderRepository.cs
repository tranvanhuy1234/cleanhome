using CleanHome.Order.Api.Models;

namespace CleanHome.Order.Api.Interfaces;

public interface ICustomerOrderRepository
{
    Task<(IReadOnlyList<OrderSummary> Items, long Total)> ListAsync(long? customerId, OrderFilter filter, CancellationToken ct);
    Task<OrderDetail?> FindAsync(long? customerId, long id, CancellationToken ct);
}
