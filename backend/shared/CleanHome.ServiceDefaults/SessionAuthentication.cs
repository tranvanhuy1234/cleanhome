using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanHome.ServiceDefaults;

public sealed record SessionIdentity(long Id, string Email, string Role, DateTime ExpiresAt);

public interface ISessionValidator
{
    Task<SessionIdentity?> ValidateAsync(string token, CancellationToken ct);
}

public static class SessionTokens
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static long UserId(this ClaimsPrincipal user) => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static string? Bearer(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : null;
    }
}

internal sealed class RemoteSessionValidator(IHttpClientFactory clients, IConfiguration configuration) : ISessionValidator
{
    public async Task<SessionIdentity?> ValidateAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            (configuration["Services:AuthUrl"] ?? "http://localhost:5001").TrimEnd('/') + "/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await clients.CreateClient("service-auth").SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
            if (!response.IsSuccessStatusCode) throw new ApiException(503, "Dịch vụ xác thực chưa sẵn sàng.");
            return await response.Content.ReadFromJsonAsync<SessionIdentity>(ct);
        }
        catch (HttpRequestException) { throw new ApiException(503, "Dịch vụ xác thực chưa sẵn sàng."); }
        catch (System.Text.Json.JsonException) { throw new ApiException(503, "Dịch vụ xác thực trả về dữ liệu không hợp lệ."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ApiException(503, "Dịch vụ xác thực phản hồi quá thời gian."); }
    }
}

internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, ISessionValidator sessions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Public endpoints không phụ thuộc auth-service kể cả khi client gửi token cũ.
        if (Context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            return AuthenticateResult.NoResult();
        var token = SessionTokens.Bearer(Request);
        if (token is null) return AuthenticateResult.NoResult();
        if (token.Length != 64 || token.Any(character => !Uri.IsHexDigit(character)))
            return AuthenticateResult.Fail("Phiên đăng nhập không hợp lệ.");
        var identity = await sessions.ValidateAsync(token, Context.RequestAborted);
        if (identity is null) return AuthenticateResult.Fail("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, identity.Id.ToString()),
            new Claim(ClaimTypes.Email, identity.Email), new Claim(ClaimTypes.Role, identity.Role)
        };
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Problem(statusCode: 401, title: "Vui lòng đăng nhập.").ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Results.Problem(statusCode: 403, title: "Bạn không có quyền thực hiện thao tác này.").ExecuteAsync(Context);
}

public static class InternalServiceAccess
{
    public static void Require(HttpRequest request, IConfiguration configuration)
    {
        var expected = configuration["InternalApi:Key"];
        var supplied = request.Headers["X-CleanHome-Service-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32 ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            throw new ApiException(403, "Yêu cầu nội bộ không hợp lệ.");
    }
}
