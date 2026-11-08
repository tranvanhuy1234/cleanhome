using CleanHome.Auth.Api.DTOs.Requests;
using CleanHome.Auth.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CleanHome.Auth.Api.Controllers;

[ApiController, Route(""), RequestSizeLimit(16384)]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("credentials")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        Response.Headers.CacheControl = "no-store";
        return Created("/api/auth/session", result);
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("credentials")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await auth.LoginAsync(request, ct));
    }

    [HttpGet("session"), Authorize]
    public async Task<IActionResult> Session(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var identity = await auth.ValidateAsync(SessionTokens.Bearer(Request)!, ct);
        return identity is null ? Unauthorized() : Ok(identity);
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(SessionTokens.Bearer(Request)!, false, User.UserId(), ct);
        return NoContent();
    }

    [HttpPost("logout-all"), Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await auth.LogoutAsync(SessionTokens.Bearer(Request)!, true, User.UserId(), ct);
        return NoContent();
    }

    [HttpPut("password"), Authorize, EnableRateLimiting("credentials")]
    public async Task<IActionResult> Password(PasswordRequest request, CancellationToken ct)
    {
        await auth.ChangeCredentialsAsync(User.UserId(), request.CurrentPassword, request.NewPassword, null, ct);
        return NoContent();
    }

    [HttpPut("email"), Authorize, EnableRateLimiting("credentials")]
    public async Task<IActionResult> Email(EmailRequest request, CancellationToken ct)
    {
        await auth.ChangeCredentialsAsync(User.UserId(), request.Password, null, request.Email, ct);
        return NoContent();
    }

    [HttpGet("accounts"), Authorize(Roles = "admin")]
    public async Task<IActionResult> Accounts([FromQuery] AccountQueryRequest query, CancellationToken ct) =>
        Ok(await auth.AccountsAsync(query, ct));

    [HttpGet("accounts/{id:long}"), Authorize(Roles = "admin")]
    public async Task<IActionResult> Account(long id, CancellationToken ct) =>
        Ok(await auth.AccountAsync(id, ct));

    [HttpPut("accounts/{id:long}"), Authorize(Roles = "admin"), EnableRateLimiting("credentials")]
    public async Task<IActionResult> UpdateAccount(long id, UpdateAccountRequest request, CancellationToken ct) =>
        Ok(await auth.UpdateAccountAsync(id, request, ct));

    [HttpPost("accounts"), Authorize(Roles = "admin"), EnableRateLimiting("credentials")]
    public async Task<IActionResult> CreateAccount(ManagedAccountRequest request, CancellationToken ct) =>
        Created("/api/auth/accounts", await auth.CreateAccountAsync(request, ct));

    [HttpPut("accounts/{id:long}/status"), Authorize(Roles = "admin")]
    public async Task<IActionResult> Status(long id, AccountStatusRequest request, CancellationToken ct)
    {
        await auth.SetStatusAsync(id, User.UserId(), request.Locked!.Value, ct);
        return NoContent();
    }
}
