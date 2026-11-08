using System.Net.Http.Headers;
using CleanHome.Order.Api.Models;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Services;
public sealed class BookingDependencies(IHttpClientFactory clients,IConfiguration configuration):IBookingDependencies
{
 private async Task<T> Get<T>(string service,string fallback,string path,string? token,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,(configuration[$"Services:{service}Url"]??fallback).TrimEnd('/')+path);
  if(token is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  if(service=="Staff")request.Headers.Add("X-CleanHome-Service-Key",configuration["InternalApi:Key"]);
  try{
   using var response=await clients.CreateClient("booking-dependencies").SendAsync(request,ct);
   if(response.StatusCode==System.Net.HttpStatusCode.NotFound)throw new ApiException(404,"Không tìm thấy dịch vụ hoặc địa chỉ thuộc tài khoản của bạn.");
   if(service=="User"&&(int)response.StatusCode is 401 or 403)throw new ApiException((int)response.StatusCode,"Phiên đăng nhập không còn hợp lệ.");
   if(!response.IsSuccessStatusCode)throw new ApiException(503,"Dịch vụ kiểm tra thông tin đặt lịch chưa sẵn sàng.");
   return await response.Content.ReadFromJsonAsync<T>(ct)??throw new ApiException(503,"Phản hồi đặt lịch không hợp lệ.");
  }catch(HttpRequestException){throw new ApiException(503,"Không kết nối được dịch vụ kiểm tra đặt lịch.");}
  catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new ApiException(503,"Kiểm tra đặt lịch quá thời gian.");}
 }
 public Task<BookingService> ServiceAsync(long id,CancellationToken ct)=>Get<BookingService>("Catalog","http://localhost:5003","/services/"+id,null,ct);
 public Task<BookingAddress> AddressAsync(long id,string token,CancellationToken ct)=>Get<BookingAddress>("User","http://localhost:5002","/me/addresses/"+id,token,ct);
 public async Task<IReadOnlyList<BookingBranch>> BranchesAsync(CancellationToken ct)
 {
  var branches=new List<BookingBranch>();
  for(var page=1;;page++){
   var result=await Get<BranchPage>("Staff","http://localhost:5005",$"/internal/branches?page={page}&pageSize=100",null,ct);
   branches.AddRange(result.Items);
   if(branches.Count>=result.Total||result.Items.Length==0)break;
  }
  return branches.Where(b=>b.Status=="active").ToArray();
 }
 private sealed record BranchPage(BookingBranch[] Items,long Total);
}
