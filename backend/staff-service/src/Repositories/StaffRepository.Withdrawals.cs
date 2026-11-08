using System.Text.Json;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using MySqlConnector;
namespace CleanHome.Staff.Api.Repositories;
public sealed partial class StaffRepository
{
 private async Task<(string Outcome,long RequestId)> Withdraw(MySqlConnection c,MySqlTransaction t,StaffWorkflowCommand command,DispatchJob? dispatch,IReadOnlyList<AssignmentSnapshot> current,CancellationToken ct)
 {
  var context=command.Context??throw new ApiException(400,"Thiếu lý do hủy nhận việc.");
  if(context.Reason.Trim().Length is <3 or >2000)throw new ApiException(400,"Nhập lý do từ 3–2000 ký tự.");
  var target=current.SingleOrDefault(a=>a.Id==command.AssignmentId)??throw new ApiException(404,"Không có phân công.");
  if(command.Kind=="withdraw"&&target.UserId!=command.ActorId)throw new ApiException(404,"Không có phần việc của bạn.");
  if(target.Status is not("accepted" or "in_progress"))throw new ApiException(409,"Chỉ hủy nhận việc còn hiệu lực, chưa hoàn thành.");
  long requestId;
  if(command.Kind=="withdraw"){
   await using(var pending=Cmd(c,t,"SELECT id FROM assignment_withdrawals WHERE assignment_id=@id AND state='pending' FOR UPDATE",("id",target.Id))){var existing=await pending.ExecuteScalarAsync(ct);if(existing is not null)return("unchanged",Convert.ToInt64(existing));}
   var minutes=await Number(c,t,"SELECT early_minutes FROM withdrawal_policy WHERE id=1",ct);
   var early=command.ScheduledStart>=DateTime.UtcNow.AddMinutes(minutes)&&!current.Any(a=>a.Status is "in_progress" or "done");
   await using(var insert=Cmd(c,t,"INSERT INTO assignment_withdrawals(assignment_id,requested_by,reason,state,requested_at,reviewed_by,reviewed_at) VALUES(@id,@actor,@reason,@state,UTC_TIMESTAMP(6),@reviewer,IF(@early,UTC_TIMESTAMP(6),NULL))",("id",target.Id),("actor",command.ActorId),("reason",context.Reason.Trim()),("state",early?"approved":"pending"),("reviewer",early?(long?)command.ActorId:null),("early",early))){await insert.ExecuteNonQueryAsync(ct);requestId=insert.LastInsertedId;}
   if(!early){await Write(c,t,"UPDATE assignments SET version=version+1 WHERE id=@id",ct,("id",target.Id));await Audit(c,t,command.BookingId,target.EmployeeId,"withdrawal_requested",ct);return("requested",requestId);}
  }else{
   requestId=context.RequestId??throw new ApiException(400,"Thiếu mã yêu cầu.");
   await using(var read=Cmd(c,t,"SELECT state FROM assignment_withdrawals WHERE id=@request AND assignment_id=@assignment FOR UPDATE",("request",requestId),("assignment",target.Id))){if(Convert.ToString(await read.ExecuteScalarAsync(ct))!="pending")throw new ApiException(409,"Yêu cầu không còn chờ duyệt.");}
   var approved=command.Kind=="withdraw_approve";
   await Write(c,t,"UPDATE assignment_withdrawals SET state=@state,reviewed_by=@actor,reviewed_at=UTC_TIMESTAMP(6),review_note=@note WHERE id=@id",ct,("state",approved?"approved":"rejected"),("actor",command.ActorId),("note",context.Reason.Trim()),("id",requestId));
   if(!approved){await Write(c,t,"UPDATE assignments SET version=version+1 WHERE id=@id",ct,("id",target.Id));await Audit(c,t,command.BookingId,target.EmployeeId,"withdrawal_rejected",ct);return("rejected",requestId);}
  }
  await Write(c,t,"UPDATE assignments SET status='cancelled',version=version+1 WHERE id=@id",ct,("id",target.Id));
  await Write(c,t,"INSERT INTO assignment_exits(assignment_id,operation_id,actor_id,source,reason,created_at) VALUES(@id,@operation,@actor,'cancelled_by_employee',@reason,UTC_TIMESTAMP(6))",ct,("id",target.Id),("operation",command.OperationId),("actor",command.ActorId),("reason",context.Reason.Trim()));
  await Audit(c,t,command.BookingId,target.EmployeeId,"employee_withdrew",ct);
  if(dispatch is not null){
   var settings=await SettingsAsync(ct);var payload=dispatch.Payload;
   // Replacement after work started covers only the remaining interval; other assignments stay intact.
   if(payload.Start<=DateTime.UtcNow)payload=payload with {Start=DateTime.UtcNow.AddMinutes(2)};
   var until=new[]{DateTime.UtcNow.AddSeconds(settings.SearchSeconds),payload.Start}.Min();
   var canSearch=payload.Start<payload.End&&until>DateTime.UtcNow;
   await Write(c,t,"UPDATE dispatch_jobs SET state=@state,payload=@payload,cycle_round=0,offer_until=NULL,search_until=@until,updated_at=UTC_TIMESTAMP(6) WHERE booking_id=@id",ct,("id",command.BookingId),("state",canSearch?"searching":"support"),("payload",JsonSerializer.Serialize(payload,Json)),("until",until));
   await Write(c,t,"UPDATE dispatch_offers SET state='closed' WHERE booking_id=@id AND state='pending'",ct,("id",command.BookingId));
   await Audit(c,t,command.BookingId,null,canSearch?"replacement_search":"replacement_support",ct);
  }
  return("withdrawn",requestId);
 }
 private static async Task RecordExits(MySqlConnection c,MySqlTransaction t,StaffWorkflowCommand command,IEnumerable<AssignmentSnapshot> targets,CancellationToken ct){foreach(var target in targets){await Write(c,t,"INSERT INTO assignment_exits(assignment_id,operation_id,actor_id,source,reason,created_at) VALUES(@id,@operation,@actor,@source,@reason,UTC_TIMESTAMP(6)) ON DUPLICATE KEY UPDATE id=id",ct,("id",target.Id),("operation",command.OperationId),("actor",command.ActorId),("source",command.Context?.Source=="customer"?"cancelled_by_customer":"cancelled_by_admin"),("reason",command.Context?.Reason??"Quản trị viên thay đổi phân công"));await Write(c,t,"UPDATE assignment_withdrawals SET state='closed',reviewed_by=@actor,reviewed_at=UTC_TIMESTAMP(6),review_note='Assignment cancelled by order workflow' WHERE assignment_id=@id AND state='pending'",ct,("id",target.Id),("actor",command.ActorId));}}
}
