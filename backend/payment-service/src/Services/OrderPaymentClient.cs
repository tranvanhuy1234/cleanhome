using System.Net.Http.Json;
using CleanHome.Payment.Api.Models;
using CleanHome.ServiceDefaults;
namespace CleanHome.Payment.Api.Services;
public sealed class OrderPaymentClient(IHttpClientFactory clients,IConfiguration config){
 public Task<OrderFact> Fact(long id,CancellationToken ct)=>Get<OrderFact>("/internal/payments/"+id,ct);
 public Task<Assignment[]> Assignments(long id,CancellationToken ct)=>Get<Assignment[]>("/internal/payments/"+id+"/assignments",ct);
 public async Task Mirror(PaymentOrder order,CancellationToken ct){using var req=Request(HttpMethod.Post,"/internal/payments/"+order.BookingId);req.Content=JsonContent.Create(new{order.Status,order.Revision,order.PaidAt});using var response=await Send(req,ct);}
 private async Task<T> Get<T>(string path,CancellationToken ct){using var req=Request(HttpMethod.Get,path);using var response=await Send(req,ct);return await response.Content.ReadFromJsonAsync<T>(ct)??throw new ApiException(503,"Dữ liệu đơn chưa sẵn sàng.");}
 private HttpRequestMessage Request(HttpMethod method,string path){var req=new HttpRequestMessage(method,(config["Services:OrderUrl"]??"http://localhost:5004").TrimEnd('/')+path);req.Headers.Add("X-CleanHome-Service-Key",config["InternalApi:Key"]);return req;}
 private async Task<HttpResponseMessage> Send(HttpRequestMessage request,CancellationToken ct){try{var response=await clients.CreateClient("payment-internal").SendAsync(request,ct);if(response.IsSuccessStatusCode)return response;var code=(int)response.StatusCode;response.Dispose();throw new ApiException(code==404?404:503,"Chưa đọc/đồng bộ được đơn hàng.");}catch(HttpRequestException){throw new ApiException(503,"Dịch vụ đơn hàng chưa sẵn sàng.");}}
}
