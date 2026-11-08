using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Repositories;
public sealed record OrderPaymentFact(long BookingId,long CustomerId,string Code,decimal Amount,string Method,string PaymentStatus,string WorkStatus,DateTime Start,bool CancellationPending);
public sealed record PaymentMirror(string Status,long Revision,DateTime? PaidAt);
public sealed class OrderPaymentRepository(DatabaseConnectionFactory connections)
{
 public async Task<OrderPaymentFact> FactAsync(long id,CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT b.customer_id,b.code,b.total_amount,p.method,p.status,b.status,b.scheduled_start,EXISTS(SELECT 1 FROM cancellation_requests r WHERE r.booking_id=b.id AND r.status='pending') FROM bookings b JOIN booking_payment_state p ON p.booking_id=b.id WHERE b.id=@id",("id",id));await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new ApiException(404,"Đơn chưa sử dụng luồng thanh toán mới.");return new(id,r.GetInt64(0),r.GetString(1),r.GetDecimal(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetDateTime(6),r.GetBoolean(7));}
 public async Task MirrorAsync(long id,PaymentMirror message,CancellationToken ct){
  if(message.Revision<1||message.Status is not ("UNPAID" or "PENDING" or "FAILED" or "PAID" or "DISPUTED" or "CASH_AWAITING_CONFIRMATION" or "REFUND_PENDING" or "REFUNDED"))throw new ApiException(400,"Trạng thái thanh toán không hợp lệ.");
  await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);
  await using(var q=c.Sql("SELECT id FROM bookings WHERE id=@id FOR UPDATE",("id",id))){q.Transaction=t;if(await q.ExecuteScalarAsync(ct)is null)throw new ApiException(404,"Không có đơn.");}
  await using(var q=c.Sql("SELECT revision FROM booking_payment_state WHERE booking_id=@id FOR UPDATE",("id",id))){q.Transaction=t;var old=await q.ExecuteScalarAsync(ct);if(old is null)throw new ApiException(404,"Không có thanh toán.");if(Convert.ToInt64(old)>=message.Revision){await t.CommitAsync(ct);return;}}
  async Task Write(string sql,params(string,object?)[] args){await using var q=c.Sql(sql,args);q.Transaction=t;await q.ExecuteNonQueryAsync(ct);}
  bool statusChanged;await using(var previous=c.Sql("SELECT status FROM booking_payment_state WHERE booking_id=@id",("id",id))){previous.Transaction=t;statusChanged=Convert.ToString(await previous.ExecuteScalarAsync(ct))!=message.Status;}
  await Write("UPDATE booking_payment_state SET status=@status,revision=@revision,paid_at=@paid,enrolled=TRUE,updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",("status",message.Status),("revision",message.Revision),("paid",message.PaidAt),("id",id));
  if(statusChanged)await Write("INSERT INTO booking_status_history(booking_id,changed_by,status,note,created_at) SELECT id,0,status,@note,UTC_TIMESTAMP(6) FROM bookings WHERE id=@id",("id",id),("note","Thanh toán: "+message.Status));
  if(message.Status=="PAID"){
   await Write("UPDATE bookings SET status='pending',version=version+1,updated_at=UTC_TIMESTAMP(6) WHERE id=@id AND status='pending_payment'",("id",id));
   // Dispatch deadline starts after payment, not before the customer visited the bank.
   await Write("UPDATE booking_dispatch SET state='queued',payment_state='paid',updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id AND state='awaiting_payment' AND EXISTS(SELECT 1 FROM bookings b WHERE b.id=@id AND b.status='pending')",("id",id));
  }
  await t.CommitAsync(ct);
 }
 public async Task<IReadOnlyList<long>> UnenrolledAsync(CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT booking_id FROM booking_payment_state WHERE enrolled=FALSE ORDER BY booking_id LIMIT 20");await using var r=await q.ExecuteReaderAsync(ct);var ids=new List<long>();while(await r.ReadAsync(ct))ids.Add(r.GetInt64(0));return ids;}
 public async Task MarkEnrolledAsync(long id,CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("UPDATE booking_payment_state SET enrolled=TRUE WHERE booking_id=@id",("id",id));await q.ExecuteNonQueryAsync(ct);}
}
