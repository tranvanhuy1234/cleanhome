using CleanHome.Staff.Api.Interfaces;
namespace CleanHome.Staff.Api.Services;
public sealed class DispatchWorker(IServiceScopeFactory scopes,ILogger<DispatchWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {while(!stoppingToken.IsCancellationRequested){try{using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<IDispatchRepository>().TickDispatchAsync(stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}catch(Exception){logger.LogWarning("Chưa hoàn thành vòng tìm nhân viên; sẽ thử lại.");}await Task.Delay(2000,stoppingToken);}}
}
