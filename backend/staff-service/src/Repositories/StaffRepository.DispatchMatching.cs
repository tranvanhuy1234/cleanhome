using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using MySqlConnector;
namespace CleanHome.Staff.Api.Repositories;
public sealed partial class StaffRepository
{
 private sealed record Candidate(long Id,long UserId,decimal? Latitude,decimal? Longitude,decimal? Accuracy,DateTime? PositionAt,int Workload,DateTime ReadySince,int PreviousOffers);
 private static double? CandidateDistance(Candidate candidate,DispatchRegistration job,DispatchSettings settings)
 {
  if(candidate.Latitude is null||candidate.Longitude is null||candidate.Accuracy is null||candidate.Accuracy>settings.MaxAccuracyMeters||candidate.PositionAt<DateTime.UtcNow.AddSeconds(-settings.PositionTtlSeconds)||candidate.PositionAt is null)return null;
  return GeoMath.DistanceKm((double)candidate.Latitude,(double)candidate.Longitude,(double)job.Latitude,(double)job.Longitude);
 }
 public async Task TickDispatchAsync(CancellationToken ct)
 {
  var settings=await SettingsAsync(ct);await using var c=await connections.OpenAsync(ct);
  await Write(c,null,"UPDATE employee_presence SET ready=FALSE,latitude=NULL,longitude=NULL,accuracy=NULL,position_at=NULL WHERE position_at<DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 24 HOUR)",ct);
  if(!settings.Enabled)return;
  var ids=new List<long>();await using(var due=c.Sql("SELECT booking_id FROM dispatch_jobs WHERE state='searching' AND (cycle_round=3 OR offer_until IS NULL OR offer_until<=UTC_TIMESTAMP(6)) ORDER BY updated_at LIMIT 20")){await using var r=await due.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ids.Add(r.GetInt64(0));}
  foreach(var id in ids){
   await using var t=await c.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,ct);var job=await LockDispatch(c,t,id,ct);if(job is null||job.State!="searching"||job.CycleRound<3&&job.OfferUntil>DateTime.UtcNow)continue;
   var d=job.Payload;var filled=await Filled(c,t,id,ct);
   await Write(c,t,"INSERT INTO dispatch_audit(booking_id,employee_id,event_type) SELECT booking_id,employee_id,'expired' FROM dispatch_offers WHERE booking_id=@id AND state='pending' AND expires_at<=UTC_TIMESTAMP(6)",ct,("id",id));
   await Write(c,t,"UPDATE dispatch_offers SET state='expired' WHERE booking_id=@id AND state='pending' AND expires_at<=UTC_TIMESTAMP(6)",ct,("id",id));
   if(filled>=d.RequiredStaff||DateTime.UtcNow>=job.SearchUntil||DateTime.UtcNow>=d.Start){var state=filled>=d.RequiredStaff?"filled":"support";await Write(c,t,"UPDATE dispatch_jobs SET state=@state,updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",ct,("state",state),("id",id));await Write(c,t,"UPDATE dispatch_offers SET state='closed' WHERE booking_id=@id AND state='pending'",ct,("id",id));await Audit(c,t,id,null,state,ct);await t.CommitAsync(ct);continue;}
   var phase=Math.Min(3,job.CycleRound+1);var round=job.CycleRound>=3?job.Round:job.Round+1;var until=new[]{DateTime.UtcNow.AddSeconds(settings.OfferSeconds),job.SearchUntil,d.Start}.Min();
   var outstanding=phase==3?(int)await Number(c,t,"SELECT COUNT(*) FROM dispatch_offers WHERE booking_id=@id AND state='pending' AND expires_at>UTC_TIMESTAMP(6)",ct,("id",id)):0;
   var allowance=Math.Max(0,settings.GroupSize-outstanding);
   if(allowance==0){await Write(c,t,"UPDATE dispatch_jobs SET updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",ct,("id",id));await t.CommitAsync(ct);continue;}
   var candidates=new List<Candidate>();
   await using(var q=Cmd(c,t,"""
    SELECT e.id,e.user_id,p.latitude,p.longitude,p.accuracy,p.position_at,
    (SELECT COUNT(*) FROM assignments a WHERE a.employee_id=e.id AND a.status<>'cancelled' AND DATE(a.scheduled_start)=DATE(@start)),p.ready_since,
    (SELECT COUNT(*) FROM dispatch_offers o WHERE o.employee_id=e.id AND o.booking_id=@booking)
    FROM employees e JOIN branches b ON b.id=e.branch_id JOIN employee_presence p ON p.employee_id=e.id
    WHERE e.branch_id=@branch AND e.status='active' AND e.verification_status='verified' AND b.status='active' AND p.ready=TRUE
    AND NOT EXISTS(SELECT 1 FROM assignments a WHERE a.employee_id=e.id AND a.status<>'cancelled' AND a.scheduled_start<@end AND a.scheduled_end>@start)
    AND EXISTS(SELECT 1 FROM employee_availability v WHERE v.employee_id=e.id AND v.status='available' AND v.starts_at<=@start AND v.ends_at>=@end)
    AND NOT EXISTS(SELECT 1 FROM employee_availability v WHERE v.employee_id=e.id AND v.status='unavailable' AND v.starts_at<@end AND v.ends_at>@start)
    AND NOT EXISTS(SELECT 1 FROM dispatch_offers o WHERE o.employee_id=e.id AND o.booking_id=@booking AND o.state='declined')
    AND NOT EXISTS(SELECT 1 FROM assignment_exits x JOIN assignments a ON a.id=x.assignment_id WHERE a.booking_id=@booking AND a.employee_id=e.id AND x.source='cancelled_by_employee')
    AND NOT EXISTS(SELECT 1 FROM dispatch_offers o WHERE o.employee_id=e.id AND o.booking_id=@booking AND o.round_number=@round)
    ORDER BY p.ready_since,e.id LIMIT 1000
    """,("booking",id),("branch",d.BranchId),("start",d.Start),("end",d.End),("round",round))){await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))candidates.Add(new(r.GetInt64(0),r.GetInt64(1),r.IsDBNull(2)?null:r.GetDecimal(2),r.IsDBNull(3)?null:r.GetDecimal(3),r.IsDBNull(4)?null:r.GetDecimal(4),r.IsDBNull(5)?null:r.GetDateTime(5),Convert.ToInt32(r.GetValue(6)),r.GetDateTime(7),Convert.ToInt32(r.GetValue(8))));}
   var active=await activeStaff.ActiveAsync(candidates.Select(a=>a.UserId).ToArray(),ct);var skills=await ServiceSkills(c,t,d.ServiceId,ct);
   var ranked=candidates.Select(e=>(Employee:e,Distance:CandidateDistance(e,d,settings))).Where(e=>active.Contains(e.Employee.UserId)&&(e.Distance.HasValue&&e.Distance<=(phase==1?settings.FirstRadiusKm:settings.SecondRadiusKm)||phase==3&&settings.AllowBranchFallback)).OrderBy(e=>e.Employee.PreviousOffers).ThenBy(e=>e.Distance??double.MaxValue).ThenBy(e=>e.Employee.Workload).ThenBy(e=>e.Employee.ReadySince).ThenBy(e=>e.Employee.Id);
   var sent=0;foreach(var candidate in ranked){var qualified=true;foreach(var skill in skills)if(await Number(c,t,"SELECT COUNT(*) FROM employee_skills WHERE employee_id=@employee AND skill_id=@skill",ct,("employee",candidate.Employee.Id),("skill",skill))==0){qualified=false;break;}if(!qualified)continue;
    await Write(c,t,"INSERT INTO dispatch_offers(booking_id,employee_id,expires_at,distance_km,round_number) VALUES(@booking,@employee,@until,@distance,@round)",ct,("booking",id),("employee",candidate.Employee.Id),("until",until),("distance",candidate.Distance.HasValue?Math.Round(candidate.Distance.Value,1):null),("round",round));await Audit(c,t,id,candidate.Employee.Id,"invited",ct);if(++sent>=allowance)break;
   }
   await Write(c,t,"UPDATE dispatch_jobs SET round_number=@round,cycle_round=@phase,offer_until=@until,updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",ct,("round",round),("phase",phase),("until",sent==0?DateTime.UtcNow.AddSeconds(2):until),("id",id));await t.CommitAsync(ct);
  }
 }
 private async Task ValidateClaim(MySqlConnection c,MySqlTransaction t,DispatchJob job,long employee,long userId,long offerId,CancellationToken ct)
 {
  var settings=await SettingsAsync(ct);var d=job.Payload;
  if(await Number(c,t,"SELECT COUNT(*) FROM assignment_exits x JOIN assignments a ON a.id=x.assignment_id WHERE a.booking_id=@booking AND a.employee_id=@employee AND x.source='cancelled_by_employee'",ct,("booking",d.BookingId),("employee",employee))>0)throw new ApiException(409,"Bạn đã hủy nhận việc; cần admin phân công lại.");
  if(await Filled(c,t,d.BookingId,ct)>=d.RequiredStaff)throw new ApiException(409,"Đơn đã có đủ nhân viên.");
  if(!settings.Enabled||job.State!="searching"||DateTime.UtcNow>=job.SearchUntil||DateTime.UtcNow>=d.Start)throw new ApiException(409,"Đơn đã đóng hoặc hết hạn tìm người.");
  await using(var q=Cmd(c,t,"SELECT state,expires_at,round_number FROM dispatch_offers WHERE id=@id AND booking_id=@booking AND employee_id=@employee FOR UPDATE",("id",offerId),("booking",d.BookingId),("employee",employee))){await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new ApiException(404,"Không có lời mời của bạn.");if(r.GetString(0)!="pending"||r.GetDateTime(1)<=DateTime.UtcNow||r.GetInt32(2)!=job.Round)throw new ApiException(409,"Lời mời đã hết hạn hoặc không còn hiệu lực.");}
  if(!(await activeStaff.ActiveAsync([userId],ct)).Contains(userId))throw new ApiException(409,"Tài khoản chưa hoạt động.");
  await using(var q=Cmd(c,t,"SELECT ready,latitude,longitude,accuracy,position_at,ready_since FROM employee_presence WHERE employee_id=@employee FOR UPDATE",("employee",employee))){await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)||!r.GetBoolean(0))throw new ApiException(409,"Bạn đã tắt sẵn sàng nhận việc.");var candidate=new Candidate(employee,userId,r.IsDBNull(1)?null:r.GetDecimal(1),r.IsDBNull(2)?null:r.GetDecimal(2),r.IsDBNull(3)?null:r.GetDecimal(3),r.IsDBNull(4)?null:r.GetDateTime(4),0,r.GetDateTime(5),0);var distance=CandidateDistance(candidate,d,settings);if((!distance.HasValue||distance>(job.CycleRound==1?settings.FirstRadiusKm:settings.SecondRadiusKm))&&!(job.CycleRound>=3&&settings.AllowBranchFallback))throw new ApiException(409,"GPS đã cũ, không chính xác hoặc ngoài khu vực. Hãy cập nhật vị trí.");}
  await RequireSlot(c,t,employee,d.Start,d.End,ct);
  if(await Number(c,t,"SELECT COUNT(*) FROM assignments WHERE employee_id=@employee AND status<>'cancelled' AND scheduled_start<@end AND scheduled_end>@start",ct,("employee",employee),("start",d.Start),("end",d.End))>0)throw new ApiException(409,"Bạn đã nhận một đơn trùng lịch.");
  foreach(var skill in await ServiceSkills(c,t,d.ServiceId,ct))if(await Number(c,t,"SELECT COUNT(*) FROM employee_skills WHERE employee_id=@employee AND skill_id=@skill",ct,("employee",employee),("skill",skill))==0)throw new ApiException(409,"Bạn chưa đáp ứng kỹ năng của dịch vụ.");
  if(DateTime.UtcNow>=job.SearchUntil||DateTime.UtcNow>=d.Start)throw new ApiException(409,"Đơn vừa hết hạn tìm nhân viên.");
 }
 private async Task AfterDispatchWorkflow(MySqlConnection c,MySqlTransaction t,DispatchJob? job,StaffWorkflowCommand command,CancellationToken ct)
 {
  if(job is null)return;var state=command.Kind is "cancel" or "admin_cancel" or "stop"?"cancelled":await Filled(c,t,command.BookingId,ct)>=job.Payload.RequiredStaff?"filled":DateTime.UtcNow<job.SearchUntil?"searching":"support";
  if(job.State=="paused"&&command.Kind is not("cancel" or "admin_cancel" or "stop"))state="paused";
  await Write(c,t,"UPDATE dispatch_jobs SET state=@state,offer_until=IF(@restart,NULL,offer_until),updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@booking",ct,("state",state),("restart",state=="searching"&&(command.Kind=="unassign"||job.State!="searching")),("booking",command.BookingId));
  if(state!="searching")await Write(c,t,"UPDATE dispatch_offers SET state='closed' WHERE booking_id=@booking AND state='pending'",ct,("booking",command.BookingId));
  if(command.Kind=="claim"){var employee=await Number(c,t,"SELECT id FROM employees WHERE user_id=@user",ct,("user",command.ActorId));await Audit(c,t,command.BookingId,employee,command.Kind,ct);}
  else if(command.Kind=="assign")foreach(var employee in command.EmployeeIds)await Audit(c,t,command.BookingId,employee,command.Kind,ct);
  else await Audit(c,t,command.BookingId,null,command.Kind,ct);
 }
}
