using CleanHome.Auth.Api.Interfaces;
using CleanHome.Auth.Api.Models;
using CleanHome.ServiceDefaults;

namespace CleanHome.Auth.Api.Repositories;

public sealed class ProfileOutboxRepository(DatabaseConnectionFactory connections) : IProfileOutboxRepository
{
    public async Task<ProfileOutboxItem?> FindPendingAsync(long accountId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var query = connection.Sql("SELECT account_id,full_name,phone FROM profile_outbox WHERE account_id=@id AND delivered_at IS NULL", ("id", accountId));
        await using var reader = await query.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new ProfileOutboxItem(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)) : null;
    }

    public async Task<IReadOnlyList<long>> ListDueAsync(CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var query = connection.Sql("SELECT account_id FROM profile_outbox WHERE delivered_at IS NULL AND next_attempt_at<=UTC_TIMESTAMP(6) ORDER BY id LIMIT 20");
        var ids = new List<long>();
        await using var reader = await query.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) ids.Add(reader.GetInt64(0));
        return ids;
    }

    public Task MarkDeliveredAsync(long accountId, CancellationToken ct) => UpdateAsync(
        "UPDATE profile_outbox SET delivered_at=UTC_TIMESTAMP(6) WHERE account_id=@id", accountId, ct);
    public Task ScheduleRetryAsync(long accountId, CancellationToken ct) => UpdateAsync(
        "UPDATE profile_outbox SET attempts=attempts+1,next_attempt_at=DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 30 SECOND) WHERE account_id=@id AND delivered_at IS NULL", accountId, ct);

    private async Task UpdateAsync(string sql, long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql(sql, ("id", id));
        await command.ExecuteNonQueryAsync(ct);
    }
}
