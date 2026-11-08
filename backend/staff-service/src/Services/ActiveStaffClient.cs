using CleanHome.ServiceDefaults;
using CleanHome.Staff.Api.Interfaces;
namespace CleanHome.Staff.Api.Services;
public sealed class ActiveStaffClient(IHttpClientFactory clients,IConfiguration configuration):IActiveStaffClient
{
 public async Task<HashSet<long>> ActiveAsync(long[] ids,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Post,(configuration["Services:AuthUrl"]??"http://localhost:5001").TrimEnd('/')+"/internal/active-staff");
  request.Headers.Add("X-CleanHome-Service-Key",configuration["InternalApi:Key"]);request.Content=JsonContent.Create(ids);
  try{using var response=await clients.CreateClient("staff-account").SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new ApiException(503,"Chưa xác minh được tài khoản nhân viên.");return (await response.Content.ReadFromJsonAsync<long[]>(ct)??[]).ToHashSet();}
  catch(HttpRequestException){throw new ApiException(503,"Dịch vụ tài khoản chưa sẵn sàng.");}
  catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new ApiException(503,"Xác minh tài khoản quá thời gian.");}
 }
}
