using Microsoft.Extensions.Diagnostics.HealthChecks;
using MySqlConnector;

namespace CleanHome.ServiceDefaults;

internal sealed class DatabaseHealthCheck(DatabaseConnectionFactory connections) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("SELECT 1", connection);
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception error) when (error is MySqlException or InvalidOperationException or ArgumentException)
        {
            // Không trả connection string, thông tin máy chủ hoặc tài khoản ra HTTP.
            return HealthCheckResult.Unhealthy("Database chưa được cấu hình hoặc chưa truy cập được.");
        }
    }
}
