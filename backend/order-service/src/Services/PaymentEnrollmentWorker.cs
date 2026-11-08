using CleanHome.Order.Api.Repositories;
namespace CleanHome.Order.Api.Services;
public sealed class PaymentEnrollmentWorker(IServiceScopeFactory scopes,IHttpClientFactory clients,IConfiguration config,ILogger<PaymentEnrollmentWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken ct){while(!ct.IsCancellationRequested){try{using var scope=scopes.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<OrderPaymentRepository>();foreach(var id in await repo.UnenrolledAsync(ct)){using var request=new HttpRequestMessage(HttpMethod.Post,(config["Services:PaymentUrl"]??"http://localhost:5006").TrimEnd('/')+"/internal/orders/"+id);request.Headers.Add("X-CleanHome-Service-Key",config["InternalApi:Key"]);using var response=await clients.CreateClient("booking-dependencies").SendAsync(request,ct);if(response.IsSuccessStatusCode)await repo.MarkEnrolledAsync(id,ct);}}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception){logger.LogWarning("Đồng bộ thanh toán sẽ thử lại.");}await Task.Delay(3000,ct);}}
}
