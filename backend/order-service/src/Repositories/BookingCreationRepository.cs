using System.Text.Json;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;
using MySqlConnector;
namespace CleanHome.Order.Api.Repositories;
public sealed class BookingCreationRepository(DatabaseConnectionFactory connections):IBookingCreationRepository
{
 private static async Task<BookingCreated?> Read(MySqlConnection connection,MySqlTransaction? transaction,long customer,string key,string hash,CancellationToken ct)
 {
  await using var query=connection.Sql("SELECT k.request_hash,b.id,b.code,b.status,b.total_amount,b.scheduled_start,b.scheduled_end FROM booking_creation_keys k LEFT JOIN bookings b ON b.id=k.booking_id WHERE k.customer_id=@customer AND k.idempotency_key=@key",("customer",customer),("key",key));query.Transaction=transaction;
  await using var r=await query.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;
  if(r.GetString(0)!=hash)throw new ApiException(409,"Khóa đặt lịch đã được dùng cho nội dung khác.");
  if(r.IsDBNull(1))return null;
  return new(r.GetInt64(1),r.GetString(2),r.GetString(3),r.GetDecimal(4),r.GetDateTime(5),r.GetDateTime(6));
 }
 public async Task<BookingCreated?> ReplayAsync(long customer,string key,string hash,CancellationToken ct)
 {await using var connection=await connections.OpenAsync(ct);return await Read(connection,null,customer,key,hash,ct);}
 public async Task<BookingCreated> CreateAsync(BookingCreation b,CancellationToken ct)
 {
  await using var connection=await connections.OpenAsync(ct);await using var tx=await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,ct);
  async Task Execute(string sql,params (string,object?)[] args){await using var command=connection.Sql(sql,args);command.Transaction=tx;await command.ExecuteNonQueryAsync(ct);}
  await Execute("INSERT INTO booking_creation_keys(customer_id,idempotency_key,request_hash) VALUES(@customer,@key,@hash) ON DUPLICATE KEY UPDATE customer_id=customer_id",("customer",b.CustomerId),("key",b.Key),("hash",b.Hash));
  var existing=await Read(connection,tx,b.CustomerId,b.Key,b.Hash,ct);if(existing is not null){await tx.CommitAsync(ct);return existing;}
  var status=b.PaymentMethod=="VNPAY"?"pending_payment":"pending";
  var code="CH-"+Guid.NewGuid().ToString("N");var total=b.Service.BasePrice*b.Quantity;
  await Execute("INSERT INTO bookings(customer_id,branch_id,address_snapshot,scheduled_start,scheduled_end,status,total_amount,code,special_request,subtotal_amount,discount_amount,created_at,updated_at) VALUES(@customer,@branch,@address,@start,@end,@status,@total,@code,@note,@total,0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))",("status",status),("customer",b.CustomerId),("branch",b.BranchId),("address",JsonSerializer.Serialize(b.Address,new JsonSerializerOptions(JsonSerializerDefaults.Web))),("start",b.Start),("end",b.End),("total",total),("code",code),("note",b.Note));
  await using var identity=connection.Sql("SELECT LAST_INSERT_ID()");identity.Transaction=tx;var id=Convert.ToInt64(await identity.ExecuteScalarAsync(ct));
  await Execute("INSERT INTO booking_items(booking_id,service_id,service_name_snapshot,quantity,unit_price,unit_snapshot) VALUES(@id,@service,@name,@quantity,@price,@unit)",("id",id),("service",b.Service.Id),("name",b.Service.Name),("quantity",b.Quantity),("price",b.Service.BasePrice),("unit",b.Service.Unit));
  await Execute("INSERT INTO booking_status_history(booking_id,changed_by,status,note,created_at) VALUES(@id,@customer,@status,'Khách hàng đặt dịch vụ, chờ xác nhận.',UTC_TIMESTAMP(6))",("status",status),("id",id),("customer",b.CustomerId));
  await Execute("UPDATE booking_creation_keys SET booking_id=@id WHERE customer_id=@customer AND idempotency_key=@key",("id",id),("customer",b.CustomerId),("key",b.Key));
  if(b.PaymentMethod is not null)await Execute("INSERT INTO booking_payment_state(booking_id,method,status) VALUES(@id,@method,@status)",("id",id),("method",b.PaymentMethod),("status",b.PaymentMethod=="VNPAY"?"PENDING":"UNPAID"));
  if(b.AutomaticDispatch)await Execute("INSERT INTO booking_dispatch(booking_id,required_staff,area_label,state) VALUES(@id,@required,@area,@state)",("id",id),("required",b.RequiredStaff),("area",b.Area),("state",b.PaymentMethod=="VNPAY"?"awaiting_payment":"queued"));
  await tx.CommitAsync(ct);return new(id,code,status,total,b.Start,b.End);
 }
}
