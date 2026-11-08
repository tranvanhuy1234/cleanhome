using System.Globalization;
using System.Text.Json;
using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Services;
public sealed class StaffDestinationService(ICustomerOrderRepository orders,IStaffWorkflowClient staff):IStaffDestinationService
{
 public async Task<StaffJobSummaryResponse> SummaryAsync(long bookingId,long staffUserId,CancellationToken ct)
 {
  if(!(await staff.AssignmentsAsync(bookingId,ct)).Any(a=>a.UserId==staffUserId))throw new ApiException(404,"Không có công việc của bạn.");
  var order=await orders.FindAsync(null,bookingId,ct)??throw new ApiException(404,"Không tìm thấy đơn.");
  return new(bookingId,order.Summary.Code,order.Summary.Status,order.Items.Select(i=>i.ServiceName).ToArray(),order.Summary.TotalAmount,order.Summary.ScheduledStart,order.Summary.ScheduledEnd,order.History.Where(h=>h.Status=="completed").Select(h=>(DateTime?)h.CreatedAt).Max());
 }
 public async Task<StaffDestinationResponse> GetAsync(long bookingId,long staffUserId,CancellationToken ct)
 {
  if(bookingId<=0)throw new ApiException(404,"Không tìm thấy công việc được phân công.");
  var assignments=await staff.AssignmentsAsync(bookingId,ct);
  if(!assignments.Any(a=>a.UserId==staffUserId&&a.Status is "accepted" or "in_progress"))throw new ApiException(404,"Chỉ xem địa chỉ sau khi đã nhận việc thành công.");
  var order=await orders.FindAsync(null,bookingId,ct);
  if(order is null||order.Summary.Status is not ("pending" or "confirmed" or "in_progress"))throw new ApiException(404,"Công việc không còn hiệu lực để dẫn đường.");
  var address=order.AddressSnapshot;
  decimal? latitude=Coordinate(address,"latitude"),longitude=Coordinate(address,"longitude");
  if(latitude is null||longitude is null||latitude is < -90 or > 90||longitude is < -180 or > 180){latitude=null;longitude=null;}
  return new(bookingId,order.Summary.Code,Text(address,"recipient"),Text(address,"phone"),Text(address,"addressText") is {Length:>0} text?text:Text(address,"address"),latitude,longitude);
 }
 private static string Text(JsonElement json,string name)=>json.ValueKind==JsonValueKind.Object&&json.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
 private static decimal? Coordinate(JsonElement json,string name)
 {
  if(json.ValueKind!=JsonValueKind.Object||!json.TryGetProperty(name,out var value))return null;
  if(value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var number))return number;
  return value.ValueKind==JsonValueKind.String&&decimal.TryParse(value.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)?parsed:null;
 }
}
