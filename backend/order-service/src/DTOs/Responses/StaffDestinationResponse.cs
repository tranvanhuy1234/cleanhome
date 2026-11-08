namespace CleanHome.Order.Api.DTOs.Responses;
public sealed record StaffDestinationResponse(long BookingId,string Code,string Recipient,string Phone,string AddressText,decimal? Latitude,decimal? Longitude);
public sealed record StaffJobSummaryResponse(long BookingId,string Code,string Status,IReadOnlyList<string> Services,decimal Amount,DateTime Start,DateTime End,DateTime? CompletedAt);
