using System.Text.Json;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using CleanHome.Staff.Api.Interfaces;
using CleanHome.Staff.Api.DTOs.Requests;
using CleanHome.Staff.Api.DTOs.Responses;
using MySqlConnector;
namespace CleanHome.Staff.Api.Repositories;
public sealed partial class StaffRepository
{
 public async Task<IReadOnlyList<long>> ServiceSkillsAsync(long serviceId,CancellationToken ct){await using var c=await connections.OpenAsync(ct);return await ServiceSkills(c,null,serviceId,ct);}
 public async Task ArriveAsync(long userId,long assignment,CancellationToken ct)
 {await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);if(await Number(c,t,"SELECT COUNT(*) FROM assignments a JOIN employees e ON e.id=a.employee_id WHERE a.id=@id AND e.user_id=@user AND a.status IN ('accepted','in_progress')",ct,("id",assignment),("user",userId))!=1)throw new ApiException(404,"Không tìm thấy công việc đã nhận.");await Write(c,t,"INSERT INTO assignment_arrivals(assignment_id,arrived_at) VALUES(@id,UTC_TIMESTAMP(6)) ON DUPLICATE KEY UPDATE assignment_id=assignment_id",ct,("id",assignment));await t.CommitAsync(ct);}
 public async Task SupportAsync(long userId,long assignment,string reason,CancellationToken ct)
 {if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length is <3 or >2000)throw new ApiException(400,"Nhập lý do hỗ trợ từ 3–2000 ký tự.");await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);var employee=await Number(c,t,"SELECT id FROM employees WHERE user_id=@user FOR UPDATE",ct,("user",userId));if(employee==0||await Number(c,t,"SELECT COUNT(*) FROM assignments WHERE id=@id AND employee_id=@employee AND status IN ('accepted','in_progress')",ct,("id",assignment),("employee",employee))!=1)throw new ApiException(404,"Không có công việc đã nhận.");await Write(c,t,"INSERT INTO assignment_support(assignment_id,reason) SELECT @id,@reason WHERE NOT EXISTS(SELECT 1 FROM assignment_support WHERE assignment_id=@id AND status='pending')",ct,("id",assignment),("reason",reason.Trim()));await t.CommitAsync(ct);}
 public async Task<object> SupportListAsync(CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT s.id,s.assignment_id,a.booking_id,a.employee_id,s.reason,s.status,s.created_at FROM assignment_support s JOIN assignments a ON a.id=s.assignment_id WHERE s.status='pending' ORDER BY s.id LIMIT 200");return await q.RowsAsync(ct);}
 public async Task ResolveSupportAsync(long id,CancellationToken ct){await using var c=await connections.OpenAsync(ct);await Write(c,null,"UPDATE assignment_support SET status='resolved',resolved_at=UTC_TIMESTAMP(6) WHERE id=@id AND status='pending'",ct,("id",id));}
 private sealed record DispatchJob(DispatchRegistration Payload,string State,int Round,DateTime? OfferUntil,DateTime SearchUntil,int CycleRound);
 private static async Task<DispatchJob?> LockDispatch(MySqlConnection c,MySqlTransaction t,long id,CancellationToken ct)
 {
  await Write(c,t,"INSERT INTO dispatch_booking_locks(booking_id) VALUES(@id) ON DUPLICATE KEY UPDATE booking_id=booking_id",ct,("id",id));
  await Number(c,t,"SELECT booking_id FROM dispatch_booking_locks WHERE booking_id=@id FOR UPDATE",ct,("id",id));
  await using var q=Cmd(c,t,"SELECT payload,state,round_number,offer_until,search_until,cycle_round FROM dispatch_jobs WHERE booking_id=@id FOR UPDATE",("id",id));
  await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new(JsonSerializer.Deserialize<DispatchRegistration>(r.GetString(0),Json)!,r.GetString(1),r.GetInt32(2),r.IsDBNull(3)?null:r.GetDateTime(3),r.GetDateTime(4),r.GetInt32(5)):null;
 }
 private static async Task<long> Number(MySqlConnection c,MySqlTransaction? t,string sql,CancellationToken ct,params(string,object?)[] values){await using var q=c.Sql(sql,values);q.Transaction=t;return Convert.ToInt64(await q.ExecuteScalarAsync(ct));}
 private static async Task Write(MySqlConnection c,MySqlTransaction? t,string sql,CancellationToken ct,params(string,object?)[] values){await using var q=c.Sql(sql,values);q.Transaction=t;await q.ExecuteNonQueryAsync(ct);}
 private static Task Audit(MySqlConnection c,MySqlTransaction t,long booking,long? employee,string type,CancellationToken ct)=>Write(c,t,"INSERT INTO dispatch_audit(booking_id,employee_id,event_type) VALUES(@booking,@employee,@type)",ct,("booking",booking),("employee",employee),("type",type));
 public async Task<DispatchSettings> SettingsAsync(CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT body FROM dispatch_settings WHERE id=1");return JsonSerializer.Deserialize<DispatchSettings>(Convert.ToString(await q.ExecuteScalarAsync(ct))!,Json)!;}
 public async Task SaveSettingsAsync(DispatchSettings settings,CancellationToken ct){await using var c=await connections.OpenAsync(ct);await Write(c,null,"UPDATE dispatch_settings SET body=@body WHERE id=1",ct,("body",JsonSerializer.Serialize(settings,Json)));}
 public async Task<PresenceResponse> PresenceAsync(long userId,CancellationToken ct)
 {
  var settings=await SettingsAsync(ct);await using var c=await connections.OpenAsync(ct);
  await using var q=c.Sql("SELECT p.ready,p.latitude,p.longitude,p.accuracy,p.position_at FROM employees e LEFT JOIN employee_presence p ON p.employee_id=e.id WHERE e.user_id=@user",("user",userId));await using var r=await q.ExecuteReaderAsync(ct);
  if(!await r.ReadAsync(ct))throw new ApiException(404,"Chưa có hồ sơ nhân viên.");
  var at=r.IsDBNull(4)?(DateTime?)null:r.GetDateTime(4);return new(!r.IsDBNull(0)&&r.GetBoolean(0),r.IsDBNull(1)?null:r.GetDecimal(1),r.IsDBNull(2)?null:r.GetDecimal(2),r.IsDBNull(3)?null:r.GetDecimal(3),at,at>=DateTime.UtcNow.AddSeconds(-settings.PositionTtlSeconds));
 }
 public async Task<PresenceResponse> SavePresenceAsync(long userId,PresenceRequest request,CancellationToken ct)
 {
  var settings=await SettingsAsync(ct);
  if(request.Ready){
   if(!request.Latitude.HasValue&&!settings.AllowBranchFallback)throw new ApiException(400,"Cần GPS hợp lệ để bật sẵn sàng.");
   if(request.Latitude.HasValue&&(request.Accuracy>settings.MaxAccuracyMeters||request.Accuracy is null||request.ObservedAt is null||request.ObservedAt<DateTimeOffset.UtcNow.AddSeconds(-settings.PositionTtlSeconds)||request.ObservedAt>DateTimeOffset.UtcNow.AddMinutes(1)))throw new ApiException(400,"GPS chưa đủ chính xác hoặc đã cũ. Hãy cập nhật vị trí.");
  }
  await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);
  var employee=await Number(c,t,"SELECT id FROM employees WHERE user_id=@user FOR UPDATE",ct,("user",userId));if(employee==0)throw new ApiException(404,"Chưa có hồ sơ nhân viên.");
  if(request.Ready&&await Number(c,t,"SELECT COUNT(*) FROM employees e JOIN branches b ON b.id=e.branch_id WHERE e.id=@id AND e.status='active' AND e.verification_status='verified' AND b.status='active'",ct,("id",employee))!=1)throw new ApiException(409,"Hồ sơ chưa hoạt động hoặc chưa được xác minh.");
  if(request.Ready&&await Number(c,t,"SELECT COUNT(*) FROM employee_presence WHERE employee_id=@id AND updated_at>DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 10 SECOND)",ct,("id",employee))>0)throw new ApiException(429,"Chờ 10 giây trước khi cập nhật GPS tiếp.");
  await Write(c,t,"""
   INSERT INTO employee_presence(employee_id,ready,latitude,longitude,accuracy,position_at) VALUES(@id,@ready,@lat,@lng,@accuracy,@at)
   ON DUPLICATE KEY UPDATE ready_since=IF(ready=FALSE AND @ready=TRUE,UTC_TIMESTAMP(6),ready_since),ready=@ready,latitude=@lat,longitude=@lng,accuracy=@accuracy,position_at=@at,updated_at=UTC_TIMESTAMP(6)
   """,ct,("id",employee),("ready",request.Ready),("lat",request.Ready?request.Latitude:null),("lng",request.Ready?request.Longitude:null),("accuracy",request.Ready?request.Accuracy:null),("at",request.Ready&&request.Latitude.HasValue?request.ObservedAt?.UtcDateTime:null));
  await t.CommitAsync(ct);return await PresenceAsync(userId,ct);
 }
 public async Task RegisterDispatchAsync(DispatchRegistration request,CancellationToken ct)
 {
  if(request.BookingId<=0||request.RequiredStaff is <1 or >20||request.Latitude is <-90 or >90||request.Longitude is <-180 or >180||request.End<=request.Start)throw new ApiException(400,"Yêu cầu tìm nhân viên không hợp lệ.");
  var settings=await SettingsAsync(ct);await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);
  await LockDispatch(c,t,request.BookingId,ct);
  if(await Number(c,t,"SELECT stopped FROM dispatch_booking_locks WHERE booking_id=@id",ct,("id",request.BookingId))!=0)throw new ApiException(409,"Đơn đã dừng tuyển người.");
  var deadline=new[]{(request.CreatedAt??DateTime.UtcNow).AddSeconds(settings.SearchSeconds),request.Start}.Min();
  await Write(c,t,"INSERT INTO dispatch_jobs(booking_id,payload,required_staff,search_until) VALUES(@id,@body,@required,@until) ON DUPLICATE KEY UPDATE booking_id=booking_id",ct,("id",request.BookingId),("body",JsonSerializer.Serialize(request,Json)),("required",request.RequiredStaff),("until",deadline));
  var old=await LockDispatch(c,t,request.BookingId,ct);if(old?.Payload!=request)throw new ApiException(409,"Đơn đã có thông tin tuyển người khác.");await t.CommitAsync(ct);
 }
 private static async Task<int> Filled(MySqlConnection c,MySqlTransaction? t,long booking,CancellationToken ct)=>checked((int)await Number(c,t,"SELECT COUNT(*) FROM assignments WHERE booking_id=@id AND status<>'cancelled'",ct,("id",booking)));
 public async Task<DispatchStatus?> DispatchStatusAsync(long bookingId,CancellationToken ct)
 {
  var settings=await SettingsAsync(ct);
  await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);var job=await LockDispatch(c,t,bookingId,ct);if(job is null)return null;
  var filled=await Filled(c,t,bookingId,ct);var accepted=(int)await Number(c,t,"SELECT COUNT(*) FROM assignments WHERE booking_id=@id AND status IN ('accepted','in_progress','done')",ct,("id",bookingId));
  return new(bookingId,!settings.Enabled&&job.State=="searching"?"paused":job.State,job.Payload.RequiredStaff,filled,accepted,job.Round,job.OfferUntil,job.SearchUntil);
 }
 public async Task<IReadOnlyList<DispatchStatus>> DispatchListAsync(CancellationToken ct)
 {
  await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT booking_id FROM dispatch_jobs ORDER BY updated_at DESC LIMIT 200");var ids=new List<long>();await using(var r=await q.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))ids.Add(r.GetInt64(0));
  var result=new List<DispatchStatus>();foreach(var id in ids){var status=await DispatchStatusAsync(id,ct);if(status is not null)result.Add(status);}return result;
 }
 private async Task<IReadOnlyList<long>> ServiceSkills(MySqlConnection c,MySqlTransaction? t,long service,CancellationToken ct)
 {await using var q=c.Sql("SELECT skill_ids FROM dispatch_service_rules WHERE service_id=@id",("id",service));q.Transaction=t;var raw=await q.ExecuteScalarAsync(ct);return raw is null or DBNull?[]:JsonSerializer.Deserialize<long[]>(Convert.ToString(raw)!)!;}
 public async Task SaveServiceSkillsAsync(long serviceId,long[] skills,CancellationToken ct)
 {if(serviceId<=0||skills.Length>50||skills.Any(s=>s<=0))throw new ApiException(400,"Kỹ năng không hợp lệ.");await using var c=await connections.OpenAsync(ct);foreach(var skill in skills)if(await Number(c,null,"SELECT COUNT(*) FROM skills WHERE id=@id",ct,("id",skill))!=1)throw new ApiException(400,"Không tìm thấy kỹ năng.");await Write(c,null,"INSERT INTO dispatch_service_rules(service_id,skill_ids) VALUES(@id,@skills) ON DUPLICATE KEY UPDATE skill_ids=@skills",ct,("id",serviceId),("skills",JsonSerializer.Serialize(skills.Distinct())));}
 public async Task<IReadOnlyList<DispatchOffer>> OffersAsync(long userId,CancellationToken ct)
 {
  if(!(await SettingsAsync(ct)).Enabled)return [];
  await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("""
   SELECT o.id,j.payload,o.distance_km,o.expires_at,o.state,
   (SELECT COUNT(*) FROM assignments a WHERE a.booking_id=j.booking_id AND a.status<>'cancelled')
   FROM dispatch_offers o JOIN dispatch_jobs j ON j.booking_id=o.booking_id JOIN employees e ON e.id=o.employee_id
   JOIN employee_presence p ON p.employee_id=e.id
   WHERE e.user_id=@user AND e.status='active' AND e.verification_status='verified' AND p.ready=TRUE
   AND o.state='pending' AND o.expires_at>UTC_TIMESTAMP(6) AND j.state='searching' ORDER BY o.id
   """,("user",userId));var result=new List<DispatchOffer>();await using var r=await q.ExecuteReaderAsync(ct);
  while(await r.ReadAsync(ct)){var d=JsonSerializer.Deserialize<DispatchRegistration>(r.GetString(1),Json)!;var remaining=Math.Max(0,d.RequiredStaff-Convert.ToInt32(r.GetValue(5)));if(remaining>0)result.Add(new(r.GetInt64(0),d.BookingId,d.Code,d.ServiceName,d.Area,d.Start,d.End,d.Amount,decimal.Round(d.Amount/d.RequiredStaff,2),d.RequiredStaff,remaining,r.IsDBNull(2)?null:r.GetDecimal(2),r.GetDateTime(3),r.GetString(4)));}return result;
 }
 public async Task<DispatchOffer> OfferAsync(long userId,long id,CancellationToken ct)
 {
  // Accepted invitations remain resolvable for durable claim retries, without exposing location.
  await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT o.id,j.payload,o.distance_km,o.expires_at,o.state,(SELECT COUNT(*) FROM assignments a WHERE a.booking_id=j.booking_id AND a.status<>'cancelled') FROM dispatch_offers o JOIN dispatch_jobs j ON j.booking_id=o.booking_id JOIN employees e ON e.id=o.employee_id WHERE o.id=@id AND e.user_id=@user",("id",id),("user",userId));await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new ApiException(404,"Không tìm thấy lời mời của bạn.");var d=JsonSerializer.Deserialize<DispatchRegistration>(r.GetString(1),Json)!;return new(id,d.BookingId,d.Code,d.ServiceName,d.Area,d.Start,d.End,d.Amount,decimal.Round(d.Amount/d.RequiredStaff,2),d.RequiredStaff,Math.Max(0,d.RequiredStaff-Convert.ToInt32(r.GetValue(5))),r.IsDBNull(2)?null:r.GetDecimal(2),r.GetDateTime(3),r.GetString(4));
 }
 public async Task SkipAsync(long userId,long id,CancellationToken ct)
 {var offer=await OfferAsync(userId,id,ct);await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);var job=await LockDispatch(c,t,offer.BookingId,ct);if(job is null)throw new ApiException(404,"Không tìm thấy đơn.");var employee=await Number(c,t,"SELECT employee_id FROM dispatch_offers WHERE id=@id FOR UPDATE",ct,("id",id));await Write(c,t,"UPDATE dispatch_offers SET state='declined',responded_at=UTC_TIMESTAMP(6) WHERE id=@id AND state='pending' AND expires_at>UTC_TIMESTAMP(6)",ct,("id",id));await Audit(c,t,offer.BookingId,employee,"declined",ct);await t.CommitAsync(ct);}
 public async Task ControlDispatchAsync(long bookingId,string action,CancellationToken ct)
 {
  if(action is not ("pause" or "resume"))throw new ApiException(400,"Thao tác không hợp lệ.");var settings=await SettingsAsync(ct);await using var c=await connections.OpenAsync(ct);await using var t=await c.BeginTransactionAsync(ct);var job=await LockDispatch(c,t,bookingId,ct)??throw new ApiException(404,"Không có tuyển người tự động.");if(job.State=="cancelled")throw new ApiException(409,"Đơn đã hủy.");
  await Write(c,t,"UPDATE dispatch_jobs SET state=@state,cycle_round=0,offer_until=NULL,search_until=@until,updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",ct,("state",action=="pause"?"paused":"searching"),("until",DateTime.UtcNow.AddSeconds(settings.SearchSeconds)),("id",bookingId));await Write(c,t,"UPDATE dispatch_offers SET state='closed' WHERE booking_id=@id AND state='pending'",ct,("id",bookingId));await Audit(c,t,bookingId,null,action,ct);await t.CommitAsync(ct);
 }
 public async Task<object> DispatchAuditAsync(long bookingId,CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT id,employee_id,event_type,created_at FROM dispatch_audit WHERE booking_id=@id ORDER BY id DESC LIMIT 200",("id",bookingId));return await q.RowsAsync(ct);}
 public async Task<object> ReadyEmployeesAsync(CancellationToken ct){await using var c=await connections.OpenAsync(ct);await using var q=c.Sql("SELECT e.branch_id,COUNT(*) AS ready_count FROM employee_presence p JOIN employees e ON e.id=p.employee_id WHERE p.ready=TRUE AND e.status='active' AND e.verification_status='verified' GROUP BY e.branch_id");return await q.RowsAsync(ct);}
}
