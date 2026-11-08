namespace CleanHome.User.Api.DTOs.Responses;

public sealed record ProfileResponse(long UserId, string FullName, string Phone, string? AvatarUrl, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record AddressResponse(long Id, string Recipient, string Phone, string AddressText, decimal Latitude, decimal Longitude);
