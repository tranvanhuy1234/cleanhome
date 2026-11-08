namespace CleanHome.Staff.Api.DTOs.Responses;
public sealed record PresenceResponse(bool Ready,decimal? Latitude,decimal? Longitude,decimal? Accuracy,DateTime? PositionAt,bool Fresh);
