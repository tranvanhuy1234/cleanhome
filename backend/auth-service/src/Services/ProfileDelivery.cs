using System.Net.Http.Json;
using CleanHome.Auth.Api.Configuration;
using CleanHome.Auth.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace CleanHome.Auth.Api.Services;

public sealed class ProfileDelivery(IProfileOutboxRepository outbox, IHttpClientFactory clients,
    IOptions<ProfileDeliveryOptions> options, ILogger<ProfileDelivery> logger) : IProfileDelivery
{
    public async Task<bool> DeliverAsync(long accountId, CancellationToken ct)
    {
        try
        {
            var item = await outbox.FindPendingAsync(accountId, ct);
            if (item is null) return true;
            using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.UserUrl.TrimEnd('/') + "/internal/profiles")
            {
                Content = JsonContent.Create(new { userId = item.AccountId, fullName = item.FullName, phone = item.Phone })
            };
            request.Headers.Add("X-CleanHome-Service-Key", options.Value.InternalKey);
            using var response = await clients.CreateClient("profile-delivery").SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                await outbox.MarkDeliveredAsync(accountId, ct);
                return true;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (ct.IsCancellationRequested) return false;
            logger.LogWarning("Chưa chuyển được hồ sơ của tài khoản {AccountId}; sẽ thử lại.", accountId);
        }
        try { await outbox.ScheduleRetryAsync(accountId, ct); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { logger.LogWarning("Chưa ghi được lịch retry hồ sơ {AccountId}; bản ghi outbox vẫn được giữ.", accountId); }
        return false;
    }
}
