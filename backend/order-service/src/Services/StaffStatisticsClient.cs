using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
namespace CleanHome.Order.Api.Services;
public sealed class StaffStatisticsClient(IHttpClientFactory clients,IConfiguration configuration):IStaffStatisticsClient
{
 public async Task<IReadOnlyList<StaffStatisticsAssignment>> GetAsync(long userId,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,(configuration["Services:StaffUrl"]??"http://localhost:5005").TrimEnd('/')+"/internal/statistics/"+userId);
  request.Headers.Add("X-CleanHome-Service-Key",configuration["InternalApi:Key"]);
  try{
   using var response=await clients.CreateClient("staff-workflow").SendAsync(request,ct);
   if(!response.IsSuccessStatusCode)throw new ApiException(503,"Chưa tải được dữ liệu nhân viên. Vui lòng thử lại.");
   return await response.Content.ReadFromJsonAsync<StaffStatisticsAssignment[]>(ct)??throw new ApiException(503,"Dữ liệu thống kê nhân viên không hợp lệ.");
  }catch(HttpRequestException){throw new ApiException(503,"Không kết nối được dịch vụ nhân viên.");}
  catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new ApiException(503,"Dịch vụ thống kê phản hồi quá thời gian.");}
 }
}
