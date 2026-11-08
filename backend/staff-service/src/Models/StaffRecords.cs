using System.Text.Json;

namespace CleanHome.Staff.Api.Models;

public sealed record BranchRecord(long Id, string Name, string AddressText, JsonElement ServiceArea, decimal Latitude, decimal Longitude, string Status);
public sealed record EmployeeRecord(long Id, long UserId, long BranchId, string VerificationStatus, int ExperienceYears, decimal ResponseRate, string Status);
public sealed record AvailabilityRecord(long Id, long EmployeeId, DateTime StartsAt, DateTime EndsAt, string Status);
public sealed record SkillRecord(long Id, string Name, string Description);
public sealed record EmployeeSkillRecord(long SkillId, string Name, string Level);
