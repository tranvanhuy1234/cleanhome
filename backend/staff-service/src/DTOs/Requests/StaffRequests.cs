using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CleanHome.Staff.Api.DTOs.Requests;

public sealed class StaffPageQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [Range(1, long.MaxValue)] public long? BranchId { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BranchRequest
{
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 5)] public string AddressText { get; set; } = "";
    public JsonElement ServiceArea { get; set; }
    [Required, Range(typeof(decimal), "-90", "90")] public decimal? Latitude { get; set; }
    [Required, Range(typeof(decimal), "-180", "180")] public decimal? Longitude { get; set; }
    [Required, RegularExpression("^(active|inactive)$")] public string Status { get; set; } = "active";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EmployeeUpdateRequest
{
    [Required, Range(1, long.MaxValue)] public long? BranchId { get; set; }
    [Required, RegularExpression("^(pending|verified|rejected)$")] public string VerificationStatus { get; set; } = "pending";
    [Range(0, 80)] public int ExperienceYears { get; set; }
    [Required, RegularExpression("^(active|inactive)$")] public string Status { get; set; } = "active";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeCreateRequest : EmployeeUpdateRequest
{
    [Required, Range(1, long.MaxValue)] public long? UserId { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AvailabilityRequest
{
    [Required] public DateTimeOffset? StartsAt { get; set; }
    [Required] public DateTimeOffset? EndsAt { get; set; }
    [Required, RegularExpression("^(available|unavailable)$")] public string Status { get; set; } = "available";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SkillRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(5000)] public string Description { get; set; } = "";
}
public sealed class EmployeeSkillInput
{
    [Range(1, long.MaxValue)] public long SkillId { get; set; }
    [Required, StringLength(20)] public string Level { get; set; } = "qualified";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeSkillsRequest
{
    [Required, MaxLength(50)] public EmployeeSkillInput[] Skills { get; set; } = [];
}
