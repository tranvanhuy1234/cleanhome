using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Repositories;
public sealed class OrderArchiveRepository(DatabaseConnectionFactory connections,IHttpClientFactory clients,IConfiguration config){
 public async Task<object> Metadata(long id,CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT b.id,a.deleted_at,a.deleted_by,a.delete_reason FROM bookings b LEFT JOIN booking_archives a ON a.booking_id=b.id WHERE b.id=@id",("id",id));var row=await q.RowsAsync(ct);if(row.Count==0)throw new ApiException(404,"Không có đơn.");await using var history=c.Sql("SELECT id,actor_id,action,reason,created_at FROM order_management_audit WHERE booking_id=@id ORDER BY id DESC LIMIT 100",("id",id));return new{archive=row,history=await history.RowsAsync(ct)};}
 public async Task<object> Change(long id,long actor,bool deleted,ArchiveRequest request,CancellationToken ct){
  var reason=request.Reason.Trim();if(reason.Length is <3 or >2000)throw new ApiException(400,"Nhập lý do hợp lệ.");await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);string status;long version;bool managed;
  await using(var q=c.Sql("SELECT b.status,b.version,EXISTS(SELECT 1 FROM booking_payment_state p WHERE p.booking_id=b.id) FROM bookings b WHERE b.id=@id FOR UPDATE",("id",id))){q.Transaction=t;await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new ApiException(404,"Không có đơn.");status=r.GetString(0);version=r.GetInt64(1);managed=r.GetBoolean(2);}
  bool already;await using(var q=c.Sql("SELECT deleted_at FROM booking_archives WHERE booking_id=@id FOR UPDATE",("id",id))){q.Transaction=t;var date=await q.ExecuteScalarAsync(ct);already=date is not null&&date!=DBNull.Value;}
  if(already==deleted){await t.CommitAsync(ct);return await Metadata(id,ct);}
  if(request.ExpectedVersion is not null&&request.ExpectedVersion!=version)throw new ApiException(409,"Đơn đã thay đổi, hãy tải lại.");
  await using(var q=c.Sql("SELECT COUNT(*) FROM workflow_operations WHERE booking_id=@id AND state='pending'",("id",id))){q.Transaction=t;if(Convert.ToInt64(await q.ExecuteScalarAsync(ct))>0)throw new ApiException(409,"Đơn đang xử lý thao tác.");}
  if(deleted){
   if(status is not("cancelled" or "completed"))throw new ApiException(409,"Chỉ đưa đơn đã hủy/hoàn thành và xử lý xong nghĩa vụ vào thùng rác.");
   using var req=new HttpRequestMessage(HttpMethod.Get,(config["Services:PaymentUrl"]??"http://localhost:5006").TrimEnd('/')+$"/internal/orders/{id}/archive-safe?managed={managed.ToString().ToLowerInvariant()}&cancelled={(status=="cancelled").ToString().ToLowerInvariant()}");req.Headers.Add("X-CleanHome-Service-Key",config["InternalApi:Key"]);
   try{using var response=await clients.CreateClient("booking-dependencies").SendAsync(req,ct);if(!response.IsSuccessStatusCode)throw new ApiException(503,"Chưa kiểm tra được nghĩa vụ thanh toán.");if(await response.Content.ReadFromJsonAsync<bool>(ct)!=true)throw new ApiException(409,"Còn thanh toán chờ xác minh, tiền chưa thu, tranh chấp hoặc hoàn tiền chưa xử lý.");}catch(HttpRequestException){throw new ApiException(503,"Dịch vụ thanh toán chưa sẵn sàng để kiểm tra xóa đơn.");}
  }
  async Task Write(string sql,params(string,object?)[] args){await using var q=c.Sql(sql,args);q.Transaction=t;await q.ExecuteNonQueryAsync(ct);}
  await Write("INSERT INTO booking_archives(booking_id,deleted_at,deleted_by,delete_reason) VALUES(@id,IF(@deleted,UTC_TIMESTAMP(6),NULL),@actor,@reason) ON DUPLICATE KEY UPDATE deleted_at=IF(@deleted,UTC_TIMESTAMP(6),NULL),deleted_by=@actor,delete_reason=@reason",("id",id),("deleted",deleted),("actor",actor),("reason",reason));
  await Write("INSERT INTO order_management_audit(booking_id,actor_id,action,reason,created_at) VALUES(@id,@actor,@action,@reason,UTC_TIMESTAMP(6))",("id",id),("actor",actor),("action",deleted?"archived":"restored"),("reason",reason));
  await Write("UPDATE bookings SET version=version+1,updated_at=UTC_TIMESTAMP(6) WHERE id=@id",("id",id));await t.CommitAsync(ct);return await Metadata(id,ct);
 }
}
