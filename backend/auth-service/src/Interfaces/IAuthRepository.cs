using CleanHome.Auth.Api.Models;
using CleanHome.ServiceDefaults;

namespace CleanHome.Auth.Api.Interfaces;

public interface IAuthRepository
{
    Task<IAuthTransaction> BeginAsync(CancellationToken ct);
    Task<SessionIdentity?> FindSessionAsync(string tokenHash, CancellationToken ct);
    Task<Account?> FindByIdAsync(long id, CancellationToken ct);
    Task<(IReadOnlyList<Account> Items, long Total)> ListAsync(int page, int pageSize, string? search, CancellationToken ct);
}

// Giữ transaction xuyên các thao tác repository, không đưa ADO.NET vào service.
public interface IAuthTransaction : IAsyncDisposable
{
    Task<Account?> FindByEmailAsync(string email, CancellationToken ct);
    Task<Account?> FindByIdAsync(long id, CancellationToken ct);
    Task<long> CreateAsync(string email, string passwordHash, string role, CancellationToken ct);
    Task QueueProfileAsync(long accountId, string fullName, string phone, CancellationToken ct);
    Task AddSessionAsync(long accountId, string tokenHash, DateTime expiresAt, CancellationToken ct);
    Task SetLoginStateAsync(long accountId, int failures, DateTime? lockedUntil, string? passwordHash, CancellationToken ct);
    Task ChangeCredentialsAsync(long accountId, string email, string passwordHash, CancellationToken ct);
    Task SetStatusAsync(long accountId, string status, CancellationToken ct);
    Task RevokeSessionsAsync(long accountId, string? tokenHash, CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
}
