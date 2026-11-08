using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Repositories;
public sealed class OrderNotificationRepository(DatabaseConnectionFactory connections){
 public async Task<object> Feed(long? customer,CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT h.id,h.booking_id,b.code,h.status,h.note,h.created_at FROM booking_status_history h JOIN bookings b ON b.id=h.booking_id WHERE (@customer IS NULL OR b.customer_id=@customer) ORDER BY h.id DESC LIMIT 100",("customer",customer));return await q.RowsAsync(ct);}
}
