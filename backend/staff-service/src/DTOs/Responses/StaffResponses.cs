using System.Text.Json;

namespace CleanHome.Staff.Api.DTOs.Responses;

public sealed record BranchResponse(long Id, string Name, string AddressText, JsonElement ServiceArea, decimal Latitude, decimal Longitude, string Status);
public sealed record EmployeeResponse(long Id, long UserId, long BranchId, string VerificationStatus, int ExperienceYears, decimal ResponseRate, string Status);
public sealed record AvailabilityResponse(long Id, long EmployeeId, DateTime StartsAt, DateTime EndsAt, string Status);
public sealed record SkillResponse(long Id, string Name, string Description);
public sealed record EmployeeSkillResponse(long SkillId, string Name, string Level);
public sealed record StaffPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
