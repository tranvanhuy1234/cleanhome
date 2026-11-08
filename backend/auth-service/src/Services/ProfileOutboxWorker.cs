using CleanHome.Auth.Api.Interfaces;

namespace CleanHome.Auth.Api.Services;

public sealed class ProfileOutboxWorker(IServiceScopeFactory scopes, ILogger<ProfileOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IProfileOutboxRepository>();
                var delivery = scope.ServiceProvider.GetRequiredService<IProfileDelivery>();
                foreach (var id in await outbox.ListDueAsync(stoppingToken)) await delivery.DeliverAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Chưa xử lý được hàng đợi tạo hồ sơ; sẽ thử lại."); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
