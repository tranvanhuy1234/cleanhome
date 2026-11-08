using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Services;
public sealed class BookingCreationService(IBookingCreationRepository bookings,IBookingDependencies dependencies):IBookingCreationService
{
 public Task<IReadOnlyList<BookingBranch>> BranchesAsync(CancellationToken ct)=>dependencies.BranchesAsync(ct);
 public async Task<BookingCreated> CreateAsync(long customer,string token,CreateBookingRequest request,CancellationToken ct)
 {
  if(decimal.Truncate(request.Quantity)!=request.Quantity)throw new ApiException(400,"Số lượng phải là số nguyên từ 1 đến 100.");
  var start=request.ScheduledStart!.Value.UtcDateTime;
  var note=string.IsNullOrWhiteSpace(request.SpecialRequest)?null:request.SpecialRequest.Trim();
  var legacy=JsonSerializer.Serialize(new{request.ServiceId,request.AddressId,request.BranchId,request.Quantity,start,note});
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.AutomaticDispatch?JsonSerializer.Serialize(new{legacy,request.RequiredStaff,request.Location}):legacy)));
  if(request.PaymentMethod is not null)hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash+":"+request.PaymentMethod)));
  var replay=await bookings.ReplayAsync(customer,request.IdempotencyKey,hash,ct);
  if(replay is not null)return replay;
  if(start<DateTime.UtcNow.AddMinutes(30)||start>DateTime.UtcNow.AddDays(90))throw new ApiException(400,"Chọn lịch trước ít nhất 30 phút và trong 90 ngày tới.");
  var service=await dependencies.ServiceAsync(request.ServiceId,ct);
  if(request.PaymentMethod is not null&&(service.BasePrice*request.Quantity<=0||decimal.Truncate(service.BasePrice*request.Quantity)!=service.BasePrice*request.Quantity))throw new ApiException(400,"Số tiền thanh toán phải là số VND nguyên dương.");
  if(service.BasePrice<0||service.BasePrice*request.Quantity>9999999999.99m)throw new ApiException(400,"Tổng giá vượt giới hạn cho một đơn dịch vụ.");
  var address=await dependencies.AddressAsync(request.AddressId,token,ct);
  if(request.AutomaticDispatch)
  {
   var point=request.Location;
   if(request.RequiredStaff is <1 or >20||point is null||!point.Confirmed||point.Latitude is null or <-90 or >90||point.Longitude is null or <-180 or >180||string.IsNullOrWhiteSpace(point.AddressText)||point.AddressText.Trim().Length<5)throw new ApiException(400,"Hãy chọn ghim và xác nhận đúng địa điểm vệ sinh trước khi đặt.");
   address=address with {Latitude=point.Latitude.Value,Longitude=point.Longitude.Value,AddressText=point.AddressText.Trim()};
  }
  var branches=await dependencies.BranchesAsync(ct);
  if(!branches.Any(b=>b.Id==request.BranchId))throw new ApiException(409,"Chi nhánh không còn nhận đơn.");
  var minutes=service.DurationMinutes*(double)request.Quantity;
  if(minutes<=0||minutes>600)throw new ApiException(400,"Tổng thời lượng mỗi đơn tối đa 10 giờ.");
  var end=start.AddMinutes(minutes);
  var localStart=start.AddHours(7);var localEnd=end.AddHours(7);
  if(localStart.TimeOfDay<TimeSpan.FromHours(8)||localEnd.TimeOfDay>TimeSpan.FromHours(18)||localStart.Date!=localEnd.Date)throw new ApiException(400,"Lịch thực hiện phải nằm trong 08:00–18:00 giờ Việt Nam.");
  return await bookings.CreateAsync(new(customer,request.IdempotencyKey,hash,request.BranchId,service,address,request.Quantity,start,end,note,request.RequiredStaff,request.AutomaticDispatch,branches.First(b=>b.Id==request.BranchId).Name,request.PaymentMethod),ct);
 }
}
