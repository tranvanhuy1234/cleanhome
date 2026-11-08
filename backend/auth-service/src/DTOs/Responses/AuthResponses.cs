namespace CleanHome.Auth.Api.DTOs.Responses;

public sealed record AccountCreatedResponse(long Id, string Email, string Role, string? AccessToken, DateTime? ExpiresAt, bool ProfileReady);
public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, long Id, string Email, string Role);
public sealed record AccountResponse(long Id, string Email, string Role, string Status, DateTime CreatedAt);
public sealed record AccountPageResponse(IReadOnlyList<AccountResponse> Items, int Page, int PageSize, long Total);
