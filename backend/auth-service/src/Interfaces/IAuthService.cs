using CleanHome.Auth.Api.DTOs.Requests;
using CleanHome.Auth.Api.DTOs.Responses;
using CleanHome.ServiceDefaults;

namespace CleanHome.Auth.Api.Interfaces;

public interface IAuthService : ISessionValidator
{
    Task<AccountCreatedResponse> RegisterAsync(RegisterRequest input, CancellationToken ct);
    Task<AccountCreatedResponse> CreateAccountAsync(ManagedAccountRequest input, CancellationToken ct);
    Task<LoginResponse> LoginAsync(LoginRequest input, CancellationToken ct);
    Task LogoutAsync(string token, bool all, long id, CancellationToken ct);
    Task ChangeCredentialsAsync(long id, string password, string? newPassword, string? newEmail, CancellationToken ct);
    Task<AccountPageResponse> AccountsAsync(AccountQueryRequest query, CancellationToken ct);
    Task<AccountResponse> AccountAsync(long id, CancellationToken ct);
    Task<AccountResponse> UpdateAccountAsync(long id, UpdateAccountRequest input, CancellationToken ct);
    Task SetStatusAsync(long id, long actorId, bool locked, CancellationToken ct);
}
