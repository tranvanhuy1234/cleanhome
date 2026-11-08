using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Services;
public sealed class StaffStatisticsService(IStaffStatisticsClient staff,IStaffStatisticsOrderRepository orders):IStaffStatisticsService
{
 public async Task<StaffStatisticsResponse> GetAsync(long userId,StaffStatisticsQuery query,CancellationToken ct)
 {
  if(query.From is null||query.To is null||query.To<query.From||query.To.Value.DayNumber-query.From.Value.DayNumber>365||query.Page<1||query.PageSize is <1 or >100||query.GroupBy is not ("day" or "week" or "month" or "year"))throw new ApiException(400,"Chọn khoảng ngày hợp lệ, tối đa 366 ngày.");
  var from=query.From.Value;var to=query.To.Value;
  var source=(await staff.GetAsync(userId,ct)).DistinctBy(a=>a.BookingId).ToArray();
  var bookings=(await orders.GetAsync(source.Select(a=>a.BookingId),ct)).ToDictionary(b=>b.Id);
  var selfCancelled=0;var adminCancelled=0;var customerCancelled=0;var replacements=0;
  var completed=new List<StaffCompletedOrder>();var received=0;var incomplete=0;var cancelled=0;var waiting=0;var accepted=0;var inProgress=0;var awaiting=0;
  foreach(var assignment in source){
   if(!bookings.TryGetValue(assignment.BookingId,out var booking))continue;
   if(assignment.Status=="done"&&booking.Status=="completed"&&!booking.CompletedAt.HasValue)throw new ApiException(503,"Đơn hoàn thành chưa có mốc hoàn thành trong lịch sử; cần kiểm tra dữ liệu.");
   var done=assignment.Status=="done"&&booking.Status=="completed"&&booking.CompletedAt.HasValue;
   var eventDate=DateOnly.FromDateTime((done?booking.CompletedAt!.Value:assignment.ScheduledStart).AddHours(7));
   if(eventDate<from||eventDate>to)continue;
   if(assignment.HasAccepted||assignment.Status is "accepted" or "in_progress" or "done")received++;
   if(assignment.Replacement&&assignment.Status!="cancelled")replacements++;
   if(assignment.Status=="cancelled"||booking.Status=="cancelled"){cancelled++;if(assignment.ExitSource=="cancelled_by_employee")selfCancelled++;if(assignment.ExitSource=="cancelled_by_admin")adminCancelled++;if(assignment.ExitSource=="cancelled_by_customer")customerCancelled++;continue;}
   if(done){
    if(assignment.ParticipantCount<=0||assignment.ShareIndex<0||assignment.ShareIndex>=assignment.ParticipantCount)throw new ApiException(503,"Chưa xác định được số nhân viên để phân bổ doanh thu.");
    var cents=decimal.Truncate(booking.TotalAmount*100);var count=assignment.ParticipantCount;
    var share=(decimal.Floor(cents/count)+(assignment.ShareIndex<cents%count?1:0))/100;
    completed.Add(new(booking.Id,booking.Code,booking.CompletedAt!.Value,booking.Services,booking.TotalAmount,count,share,"completed"));
   }else{incomplete++;if(assignment.Status=="waiting")waiting++;if(assignment.Status=="accepted")accepted++;if(assignment.Status=="in_progress")inProgress++;if(assignment.Status=="done")awaiting++;}
  }
  var ordered=completed.OrderByDescending(b=>b.CompletedAt).ThenByDescending(b=>b.Id).ToArray();
  DateOnly Bucket(DateOnly date)=>query.GroupBy switch{"week"=>date.AddDays(-(((int)date.DayOfWeek+6)%7)),"month"=>new(date.Year,date.Month,1),"year"=>new(date.Year,1,1),_=>date};
  var totals=ordered.GroupBy(b=>Bucket(DateOnly.FromDateTime(b.CompletedAt.AddHours(7)))).ToDictionary(g=>g.Key,g=>(Revenue:g.Sum(b=>b.AllocatedRevenue),Count:g.Count()));
  var buckets=Enumerable.Range(0,to.DayNumber-from.DayNumber+1).Select(i=>Bucket(from.AddDays(i))).Distinct();
  var series=buckets.Select(date=>{var value=totals.GetValueOrDefault(date);return new StaffRevenuePoint(date.ToString("yyyy-MM-dd"),value.Revenue,value.Count);}).ToArray();
  return new(from,to,query.GroupBy,new(received,ordered.Length,incomplete,cancelled,waiting,accepted,inProgress,awaiting,ordered.Sum(b=>b.AllocatedRevenue),selfCancelled,adminCancelled,customerCancelled,replacements),series,new(ordered.Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToArray(),query.Page,query.PageSize,ordered.Length));
 }
}
