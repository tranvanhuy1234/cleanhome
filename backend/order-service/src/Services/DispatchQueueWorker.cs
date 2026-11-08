using CleanHome.Order.Api.Repositories;
namespace CleanHome.Order.Api.Services;
public sealed class DispatchQueueWorker(IServiceScopeFactory scopes,ILogger<DispatchQueueWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken ct){while(!ct.IsCancellationRequested){try{using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<DispatchQueueRepository>().TickAsync(ct);await scope.ServiceProvider.GetRequiredService<DispatchNotificationRepository>().Tick(ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception){logger.LogWarning("Chưa gửi được yêu cầu tìm nhân viên; sẽ thử lại từ hàng đợi bền vững.");}await Task.Delay(2000,ct);}}
}
