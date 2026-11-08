using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Repositories;
public sealed class DispatchNotificationRepository(DatabaseConnectionFactory connections,IDispatchBridge dispatch){
 public async Task Tick(CancellationToken ct){await using var c=await connections.OpenAsync(ct);var ids=new List<long>();await using(var q=c.Sql("SELECT b.id FROM bookings b JOIN booking_dispatch d ON d.booking_id=b.id LEFT JOIN booking_dispatch_notices n ON n.booking_id=b.id WHERE d.state='registered' AND b.status IN ('pending','confirmed','in_progress') ORDER BY COALESCE(n.checked_at,'2000-01-01'),b.id LIMIT 20")){await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ids.Add(r.GetInt64(0));}
  foreach(var id in ids){var status=await dispatch.StatusAsync(id,ct);if(status is null)continue;var hash=status.State+":"+status.Accepted+":"+status.RequiredStaff;await using var t=await c.BeginTransactionAsync(ct);async Task Write(string sql,params(string,object?)[] args){await using var q=c.Sql(sql,args);q.Transaction=t;await q.ExecuteNonQueryAsync(ct);}
   // Booking first: same lock order as workflow and payment mirror.
   await using(var q=c.Sql("SELECT status FROM bookings WHERE id=@id FOR UPDATE",("id",id))){q.Transaction=t;var state=Convert.ToString(await q.ExecuteScalarAsync(ct));if(state is "cancelled" or "completed")continue;}
   await Write("INSERT IGNORE INTO booking_dispatch_notices(booking_id) VALUES(@id)",("id",id));string? previous;await using(var q=c.Sql("SELECT last_hash FROM booking_dispatch_notices WHERE booking_id=@id FOR UPDATE",("id",id))){q.Transaction=t;previous=Convert.ToString(await q.ExecuteScalarAsync(ct));}
   if(previous!=hash){var text=status.State switch {"support"=>"Cần hỗ trợ nhân sự: chưa tìm đủ người trong thời hạn.","filled"=>"Đã giữ đủ vị trí nhân viên.","paused"=>"Tạm dừng tìm nhân viên.",_=>$"Đang tìm nhân viên: đã nhận {status.Accepted}/{status.RequiredStaff}."};await Write("INSERT INTO booking_status_history(booking_id,changed_by,status,note,created_at) SELECT id,0,status,@note,UTC_TIMESTAMP(6) FROM bookings WHERE id=@id",("id",id),("note",text));}
   await Write("UPDATE booking_dispatch_notices SET last_hash=@hash,checked_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",("id",id),("hash",hash));await t.CommitAsync(ct);
  }
 }
}
