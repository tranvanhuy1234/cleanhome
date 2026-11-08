namespace CleanHome.User.Api.Models;

public sealed record UserProfile(long UserId, string FullName, string Phone, string? AvatarUrl, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record Address(long Id, long UserId, string Recipient, string Phone, string AddressText, decimal Latitude, decimal Longitude);
