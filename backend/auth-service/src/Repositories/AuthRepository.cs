using CleanHome.Auth.Api.Interfaces;
using CleanHome.Auth.Api.Models;
using CleanHome.ServiceDefaults;
using MySqlConnector;

namespace CleanHome.Auth.Api.Repositories;

public sealed class AuthRepository(DatabaseConnectionFactory connections) : IAuthRepository
{
    internal const string Columns = "id,email,password_hash,role,status,failed_login_count,locked_until,created_at";
    internal static Account ReadAccount(MySqlDataReader reader) => new(
        reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
        reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetDateTime(6), reader.GetDateTime(7));

    public async Task<IAuthTransaction> BeginAsync(CancellationToken ct)
    {
        var connection = await connections.OpenAsync(ct);
        try { return new AuthTransaction(connection, await connection.BeginTransactionAsync(ct)); }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<SessionIdentity?> FindSessionAsync(string tokenHash, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql("""
            SELECT a.id,a.email,a.role,s.expires_at FROM auth_sessions s JOIN accounts a ON a.id=s.account_id
            WHERE s.token_hash=@hash AND s.revoked_at IS NULL AND s.expires_at>UTC_TIMESTAMP(6) AND a.status='active'
            """, ("hash", tokenHash));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new SessionIdentity(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)) : null;
    }

    public async Task<Account?> FindByIdAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql($"SELECT {Columns} FROM accounts WHERE id=@id", ("id", id));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadAccount(reader) : null;
    }

    public async Task<(IReadOnlyList<Account> Items, long Total)> ListAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        // Escape LIKE để %, _ và ! trong email là ký tự thường, không mở rộng kết quả.
        var pattern = search is null ? null : "%" + search.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
        long? accountId = long.TryParse(search, out var parsedId) && parsedId > 0 ? parsedId : null;
        const string filter = "WHERE (@pattern IS NULL OR (@id IS NOT NULL AND id=@id) OR (@id IS NULL AND email LIKE @pattern ESCAPE '!'))";
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var query = connection.Sql($"SELECT {Columns} FROM accounts {filter} ORDER BY id DESC LIMIT @limit OFFSET @offset",
            ("pattern", pattern), ("id", accountId), ("limit", pageSize), ("offset", (page - 1) * pageSize));
        query.Transaction = transaction;
        var items = new List<Account>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(ReadAccount(reader));
        await using var count = connection.Sql($"SELECT COUNT(*) FROM accounts {filter}", ("pattern", pattern), ("id", accountId));
        count.Transaction = transaction;
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        await transaction.CommitAsync(ct);
        return (items, total);
    }

    private sealed class AuthTransaction(MySqlConnection connection, MySqlTransaction transaction) : IAuthTransaction
    {
        private MySqlCommand Command(string sql, params (string Name, object? Value)[] values)
        {
            var command = connection.Sql(sql, values);
            command.Transaction = transaction;
            return command;
        }

        public Task<Account?> FindByEmailAsync(string email, CancellationToken ct) => FindAsync("email=@value", email, ct);
        public Task<Account?> FindByIdAsync(long id, CancellationToken ct) => FindAsync("id=@value", id, ct);
        private async Task<Account?> FindAsync(string condition, object value, CancellationToken ct)
        {
            // condition chỉ do hai hàm nội bộ cố định phía trên cung cấp.
            await using var command = Command($"SELECT {Columns} FROM accounts WHERE {condition} FOR UPDATE", ("value", value));
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadAccount(reader) : null;
        }

        public async Task<long> CreateAsync(string email, string passwordHash, string role, CancellationToken ct)
        {
            await using var command = Command("INSERT INTO accounts (email,password_hash,role) VALUES (@email,@hash,@role)",
                ("email", email), ("hash", passwordHash), ("role", role));
            await command.ExecuteNonQueryAsync(ct);
            return command.LastInsertedId;
        }

        public async Task QueueProfileAsync(long accountId, string fullName, string phone, CancellationToken ct)
        {
            await using var command = Command("INSERT INTO profile_outbox (account_id,full_name,phone) VALUES (@id,@name,@phone)",
                ("id", accountId), ("name", fullName), ("phone", phone));
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task AddSessionAsync(long accountId, string tokenHash, DateTime expiresAt, CancellationToken ct)
        {
            await using var command = Command("INSERT INTO auth_sessions (account_id,token_hash,expires_at) VALUES (@id,@hash,@expires)",
                ("id", accountId), ("hash", tokenHash), ("expires", expiresAt));
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task SetLoginStateAsync(long accountId, int failures, DateTime? lockedUntil, string? passwordHash, CancellationToken ct)
        {
            await using var command = Command("""
                UPDATE accounts SET failed_login_count=@failures,locked_until=@until,
                  password_hash=COALESCE(@hash,password_hash),updated_at=UTC_TIMESTAMP(6) WHERE id=@id
                """, ("id", accountId), ("failures", failures), ("until", lockedUntil), ("hash", passwordHash));
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task ChangeCredentialsAsync(long accountId, string email, string passwordHash, CancellationToken ct)
        {
            await using var command = Command("UPDATE accounts SET email=@email,password_hash=@hash,updated_at=UTC_TIMESTAMP(6) WHERE id=@id",
                ("id", accountId), ("email", email), ("hash", passwordHash));
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task SetStatusAsync(long accountId, string status, CancellationToken ct)
        {
            await using var command = Command("UPDATE accounts SET status=@status,updated_at=UTC_TIMESTAMP(6) WHERE id=@id", ("id", accountId), ("status", status));
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task RevokeSessionsAsync(long accountId, string? tokenHash, CancellationToken ct)
        {
            await using var command = Command("""
                UPDATE auth_sessions SET revoked_at=UTC_TIMESTAMP(6)
                WHERE account_id=@id AND revoked_at IS NULL AND (@hash IS NULL OR token_hash=@hash)
                """, ("id", accountId), ("hash", tokenHash));
            await command.ExecuteNonQueryAsync(ct);
        }

        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
