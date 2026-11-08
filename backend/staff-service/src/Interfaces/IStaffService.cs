using CleanHome.Staff.Api.DTOs.Requests;
using CleanHome.Staff.Api.DTOs.Responses;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Staff.Api.Interfaces;

public interface IStaffService
{
    Task<StaffPage<BranchResponse>> BranchesAsync(StaffPageQuery query, CancellationToken ct);
    Task<BranchResponse> BranchAsync(long id, CancellationToken ct);
    Task<BranchResponse> SaveBranchAsync(long? id, BranchRequest input, CancellationToken ct);
    Task<StaffPage<EmployeeResponse>> EmployeesAsync(StaffPageQuery query, CancellationToken ct);
    Task<EmployeeResponse> EmployeeAsync(long id, CancellationToken ct);
    Task<EmployeeResponse> MyEmployeeAsync(long userId, CancellationToken ct);
    Task<EmployeeResponse> CreateEmployeeAsync(EmployeeCreateRequest input, string token, CancellationToken ct);
    Task<EmployeeResponse> UpdateEmployeeAsync(long id, EmployeeUpdateRequest input, CancellationToken ct);
    Task<IReadOnlyList<AvailabilityResponse>> AvailabilityAsync(long id, CancellationToken ct);
    Task<AvailabilityResponse> SaveAvailabilityAsync(long employeeId, long? id, AvailabilityRequest input, CancellationToken ct);
    Task DeleteAvailabilityAsync(long employeeId, long id, CancellationToken ct);
    Task<IReadOnlyList<SkillResponse>> SkillsAsync(CancellationToken ct);
    Task<SkillResponse> SaveSkillAsync(long? id, SkillRequest input, CancellationToken ct);
    Task<IReadOnlyList<EmployeeSkillResponse>> EmployeeSkillsAsync(long employeeId, CancellationToken ct);
    Task SetSkillsAsync(long employeeId, EmployeeSkillsRequest input, CancellationToken ct);
    Task<IReadOnlyList<AssignmentSnapshot>> AssignmentsAsync(long bookingId, long? userId, CancellationToken ct);
    Task<StaffWorkflowResult> ExecuteAsync(StaffWorkflowCommand command, CancellationToken ct);
}
