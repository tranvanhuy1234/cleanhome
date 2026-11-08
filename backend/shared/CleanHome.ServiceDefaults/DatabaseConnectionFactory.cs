using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace CleanHome.ServiceDefaults;

public sealed class DatabaseConnectionFactory(IConfiguration configuration)
{
    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:Database cho service.");

        var connection = new MySqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
