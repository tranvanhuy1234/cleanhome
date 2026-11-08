using CleanHome.Auth.Api.Configuration;
using CleanHome.Auth.Api.DTOs.Requests;
using CleanHome.Auth.Api.DTOs.Responses;
using CleanHome.Auth.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CleanHome.Auth.Api.Services;

public sealed class AuthService(IAuthRepository accounts, IProfileDelivery profiles, IOptions<AuthOptions> options) : IAuthService
{
    private static readonly PasswordHasher<string> Passwords = new();
    private static readonly string DummyHash = Passwords.HashPassword("dummy", SessionTokens.Create());
    private readonly AuthOptions settings = options.Value;

    public Task<SessionIdentity?> ValidateAsync(string token, CancellationToken ct) => accounts.FindSessionAsync(SessionTokens.Hash(token), ct);
    public Task<AccountCreatedResponse> RegisterAsync(RegisterRequest input, CancellationToken ct) => CreateAsync(input, "customer", true, ct);

    public Task<AccountCreatedResponse> CreateAccountAsync(ManagedAccountRequest input, CancellationToken ct)
    {
        if (input.Role is not ("customer" or "staff")) throw new ApiException(400, "Vai trò không hợp lệ.");
        return CreateAsync(input, input.Role, false, ct);
    }

    private async Task<AccountCreatedResponse> CreateAsync(RegisterRequest input, string role, bool issueToken, CancellationToken ct)
    {
        var email = NormalizeEmail(input.Email);
        var name = input.FullName.Trim();
        if (name.Length < 2) throw new ApiException(400, "Họ tên phải có ít nhất 2 ký tự.");
        var hash = Passwords.HashPassword(email, input.Password);
        var token = issueToken ? SessionTokens.Create() : null;
        var expires = DateTime.UtcNow.AddHours(settings.SessionHours);
        long id;
        await using (var transaction = await accounts.BeginAsync(ct))
        {
            id = await transaction.CreateAsync(email, hash, role, ct);
            await transaction.QueueProfileAsync(id, name, input.Phone, ct);
            if (token is not null) await transaction.AddSessionAsync(id, SessionTokens.Hash(token), expires, ct);
            await transaction.CommitAsync(ct);
        }
        var ready = await profiles.DeliverAsync(id, ct);
        return new AccountCreatedResponse(id, email, role, token, issueToken ? expires : null, ready);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest input, CancellationToken ct)
    {
        var email = NormalizeEmail(input.Email);
        await using var transaction = await accounts.BeginAsync(ct);
        var account = await transaction.FindByEmailAsync(email, ct);
        var verification = Verify(email, account?.PasswordHash ?? DummyHash, input.Password);
        var now = DateTime.UtcNow;
        if (account is null || account.Status != "active" || account.LockedUntil > now || verification == PasswordVerificationResult.Failed)
        {
            if (account is { Status: "active" } && !(account.LockedUntil > now))
            {
                var failures = account.LockedUntil is not null ? 1 : account.FailedLoginCount + 1;
                DateTime? until = failures >= settings.LockoutThreshold ? now.AddMinutes(settings.LockoutMinutes) : null;
                await transaction.SetLoginStateAsync(account.Id, failures, until, null, ct);
            }
            await transaction.CommitAsync(ct);
            throw new ApiException(401, "Tài khoản hoặc mật khẩu không chính xác, hoặc tài khoản tạm thời không khả dụng.");
        }
        var hash = verification == PasswordVerificationResult.SuccessRehashNeeded ? Passwords.HashPassword(email, input.Password) : null;
        await transaction.SetLoginStateAsync(account.Id, 0, null, hash, ct);
        var token = SessionTokens.Create();
        var expires = now.AddHours(settings.SessionHours);
        await transaction.AddSessionAsync(account.Id, SessionTokens.Hash(token), expires, ct);
        await transaction.CommitAsync(ct);
        return new LoginResponse(token, "Bearer", expires, account.Id, email, account.Role);
    }

    public async Task LogoutAsync(string token, bool all, long id, CancellationToken ct)
    {
        await using var transaction = await accounts.BeginAsync(ct);
        await transaction.RevokeSessionsAsync(id, all ? null : SessionTokens.Hash(token), ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ChangeCredentialsAsync(long id, string password, string? newPassword, string? newEmail, CancellationToken ct)
    {
        await using var transaction = await accounts.BeginAsync(ct);
        var account = await transaction.FindByIdAsync(id, ct);
        if (account is null || account.Status != "active") throw new ApiException(401, "Tài khoản không khả dụng.");
        if (Verify(account.Email, account.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw new ApiException(400, "Mật khẩu hiện tại không chính xác.");
        var email = newEmail is null ? account.Email : NormalizeEmail(newEmail);
        var hash = newPassword is null ? account.PasswordHash : Passwords.HashPassword(email, newPassword);
        await transaction.ChangeCredentialsAsync(id, email, hash, ct);
        await transaction.RevokeSessionsAsync(id, null, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<AccountPageResponse> AccountsAsync(AccountQueryRequest query, CancellationToken ct)
    {
        if (query.Page < 1 || query.Page > 100000 || query.PageSize is < 1 or > 100)
            throw new ApiException(400, "Phân trang không hợp lệ.");
        if (query.Search?.Length > 254) throw new ApiException(400, "Từ khóa tìm kiếm không được vượt quá 254 ký tự.");
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var (items, total) = await accounts.ListAsync(query.Page, query.PageSize, search, ct);
        return new AccountPageResponse(items.Select(account => new AccountResponse(account.Id, account.Email, account.Role, account.Status, account.CreatedAt)).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<AccountResponse> AccountAsync(long id, CancellationToken ct)
    {
        if (id <= 0) throw new ApiException(400, "Mã tài khoản phải là số nguyên dương.");
        var account = await accounts.FindByIdAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy tài khoản.");
        return new AccountResponse(account.Id, account.Email, account.Role, account.Status, account.CreatedAt);
    }

    public async Task<AccountResponse> UpdateAccountAsync(long id, UpdateAccountRequest input, CancellationToken ct)
    {
        if (id <= 0) throw new ApiException(400, "Mã tài khoản phải là số nguyên dương.");
        var email = NormalizeEmail(input.Email);
        await using var transaction = await accounts.BeginAsync(ct);
        var account = await transaction.FindByIdAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy tài khoản.");
        if (account.Role == "admin")
            throw new ApiException(409, "API này chỉ quản lý tài khoản khách hàng và nhân viên. Quản trị viên đổi email cá nhân qua API /email.");
        if (!string.Equals(account.Email, email, StringComparison.Ordinal))
        {
            // Email và thu hồi phiên phải cùng transaction; email trùng sẽ rollback toàn bộ.
            await transaction.ChangeCredentialsAsync(id, email, account.PasswordHash, ct);
            await transaction.RevokeSessionsAsync(id, null, ct);
        }
        await transaction.CommitAsync(ct);
        return new AccountResponse(account.Id, email, account.Role, account.Status, account.CreatedAt);
    }

    public async Task SetStatusAsync(long id, long actorId, bool locked, CancellationToken ct)
    {
        if (id == actorId) throw new ApiException(409, "Không thể thay đổi trạng thái chính tài khoản đang đăng nhập.");
        await using var transaction = await accounts.BeginAsync(ct);
        if (await transaction.FindByIdAsync(id, ct) is null) throw new ApiException(404, "Không tìm thấy tài khoản.");
        await transaction.SetStatusAsync(id, locked ? "locked" : "active", ct);
        if (locked) await transaction.RevokeSessionsAsync(id, null, ct);
        await transaction.CommitAsync(ct);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static PasswordVerificationResult Verify(string email, string hash, string password)
    {
        try { return Passwords.VerifyHashedPassword(email, hash, password); }
        catch (FormatException) { return PasswordVerificationResult.Failed; }
    }
}
