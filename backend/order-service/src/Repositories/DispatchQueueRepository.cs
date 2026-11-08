using System.Text.Json;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
namespace CleanHome.Order.Api.Repositories;
public sealed class DispatchQueueRepository(DatabaseConnectionFactory connections,IDispatchBridge dispatch)
{
 public async Task TickAsync(CancellationToken ct)
 {
  await using var c=await connections.OpenAsync(ct);var ids=new List<long>();await using(var q=c.Sql("SELECT booking_id FROM booking_dispatch WHERE state='queued' ORDER BY booking_id LIMIT 20")){await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ids.Add(r.GetInt64(0));}
  foreach(var id in ids){
   await using var t=await c.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,ct);
   await using var read=c.Sql("SELECT b.code,b.branch_id,b.address_snapshot,b.scheduled_start,b.scheduled_end,b.total_amount,d.required_staff,b.status,d.state,d.area_label,IF(EXISTS(SELECT 1 FROM booking_payment_state p WHERE p.booking_id=b.id AND p.method='VNPAY'),d.updated_at,b.created_at) FROM bookings b JOIN booking_dispatch d ON d.booking_id=b.id WHERE b.id=@id FOR UPDATE",("id",id));read.Transaction=t;
   string code,status,queueState,area;long branch;JsonElement address;DateTime start,end,createdAt;decimal total;int required;
   await using(var r=await read.ExecuteReaderAsync(ct)){if(!await r.ReadAsync(ct))continue;code=r.GetString(0);branch=r.GetInt64(1);using var json=JsonDocument.Parse(r.GetString(2));address=json.RootElement.Clone();start=r.GetDateTime(3);end=r.GetDateTime(4);total=r.GetDecimal(5);required=r.GetInt32(6);status=r.GetString(7);queueState=r.GetString(8);area=r.GetString(9);createdAt=r.GetDateTime(10);}
   if(queueState!="queued")continue;
   if(status is "cancelled" or "completed"){await Update("closed");await t.CommitAsync(ct);continue;}
   await using var pending=c.Sql("SELECT COUNT(*) FROM cancellation_requests WHERE booking_id=@id AND status='pending'",("id",id));pending.Transaction=t;if(Convert.ToInt64(await pending.ExecuteScalarAsync(ct))>0)continue;
   await using var item=c.Sql("SELECT service_id,service_name_snapshot FROM booking_items WHERE booking_id=@id ORDER BY id LIMIT 1",("id",id));item.Transaction=t;long service;string name;await using(var r=await item.ExecuteReaderAsync(ct)){if(!await r.ReadAsync(ct))continue;service=r.GetInt64(0);name=r.GetString(1);}

   await dispatch.RegisterAsync(new(id,code,branch,area,service,name,start,end,total,required,address.GetProperty("latitude").GetDecimal(),address.GetProperty("longitude").GetDecimal(),createdAt),ct);
   await Update("registered");await t.CommitAsync(ct);
   async Task Update(string state){await using var q=c.Sql("UPDATE booking_dispatch SET state=@state,updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",("state",state),("id",id));q.Transaction=t;await q.ExecuteNonQueryAsync(ct);}
  }
 }
}
