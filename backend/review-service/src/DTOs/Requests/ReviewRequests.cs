using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CleanHome.Review.Api.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateReviewRequest
{
    [Range(1, long.MaxValue)] public long BookingId { get; set; }
    [Range(1, long.MaxValue)] public long EmployeeId { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    [Required, StringLength(2000, MinimumLength = 1)] public string Comment { get; set; } = "";
}
public class ReviewQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [Range(1, long.MaxValue)] public long? BookingId { get; set; }
    [Range(1, long.MaxValue)] public long? EmployeeId { get; set; }
    [Range(1, 5)] public int? Rating { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}
public sealed class AdminReviewQuery : ReviewQuery
{
    public bool? IsHidden { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReviewVisibilityRequest
{
    [Required] public bool? IsHidden { get; set; }
    [MaxLength(2000)] public string? Reason { get; set; }
}
