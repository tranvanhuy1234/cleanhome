namespace CleanHome.Auth.Api.Models;

// Model nội bộ, không trả trực tiếp qua API vì chứa password hash.
public sealed record Account(long Id, string Email, string PasswordHash, string Role, string Status,
    int FailedLoginCount, DateTime? LockedUntil, DateTime CreatedAt);
public sealed record ProfileOutboxItem(long AccountId, string FullName, string Phone);
