using System.Text.Json;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
namespace CleanHome.Order.Api.Services;
public sealed class DispatchBridge(IHttpClientFactory clients,IConfiguration configuration):IDispatchBridge
{
 private async Task<T?> Send<T>(string path,object? body,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,(configuration["Services:StaffUrl"]??"http://localhost:5005").TrimEnd('/')+"/internal/dispatch"+path);
  request.Headers.Add("X-CleanHome-Service-Key",configuration["InternalApi:Key"]);if(body is not null)request.Content=JsonContent.Create(body);
  try{using var response=await clients.CreateClient("staff-workflow").SendAsync(request,ct);if(!response.IsSuccessStatusCode){var code=(int)response.StatusCode;throw new ApiException(code is 400 or 404 or 409?code:503,"Chưa xử lý được yêu cầu tuyển người. Hãy tải lại trạng thái đơn.");}return response.StatusCode==System.Net.HttpStatusCode.NoContent?default:await response.Content.ReadFromJsonAsync<T>(ct);}
  catch(HttpRequestException){throw new ApiException(503,"Không kết nối được dịch vụ tuyển người.");}catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new ApiException(503,"Dịch vụ tuyển người quá thời gian; hệ thống sẽ thử lại.");}
 }
 public async Task RegisterAsync(DispatchRegistration registration,CancellationToken ct)=>_ = await Send<JsonElement>("",registration,ct);
 public Task<DispatchStatus?> StatusAsync(long bookingId,CancellationToken ct)=>Send<DispatchStatus>("/"+bookingId,null,ct);
 public async Task<DispatchOffer> OfferAsync(long userId,long offerId,CancellationToken ct)=>await Send<DispatchOffer>($"/offers/{offerId}/{userId}",null,ct)??throw new ApiException(404,"Không tìm thấy lời mời.");
}
