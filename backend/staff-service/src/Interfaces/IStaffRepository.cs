using CleanHome.Staff.Api.Models;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Staff.Api.Interfaces;

public interface IStaffRepository
{
    Task<(IReadOnlyList<BranchRecord> Items, long Total)> BranchesAsync(int page, int size, CancellationToken ct);
    Task<BranchRecord?> BranchAsync(long id, CancellationToken ct);
    Task<long> SaveBranchAsync(BranchRecord branch, bool create, CancellationToken ct);
    Task<(IReadOnlyList<EmployeeRecord> Items, long Total)> EmployeesAsync(int page, int size, long? branchId, CancellationToken ct);
    Task<EmployeeRecord?> EmployeeAsync(long id, CancellationToken ct);
    Task<EmployeeRecord?> EmployeeByUserAsync(long userId, CancellationToken ct);
    Task<long> SaveEmployeeAsync(EmployeeRecord employee, bool create, CancellationToken ct);
    Task<IReadOnlyList<AvailabilityRecord>> AvailabilityAsync(long employeeId, CancellationToken ct);
    Task<long> SaveAvailabilityAsync(AvailabilityRecord item, bool create, CancellationToken ct);
    Task DeleteAvailabilityAsync(long employeeId, long id, CancellationToken ct);
    Task<IReadOnlyList<SkillRecord>> SkillsAsync(CancellationToken ct);
    Task<long> SaveSkillAsync(SkillRecord skill, bool create, CancellationToken ct);
    Task<IReadOnlyList<EmployeeSkillRecord>> EmployeeSkillsAsync(long employeeId, CancellationToken ct);
    Task SetEmployeeSkillsAsync(long employeeId, IReadOnlyList<(long Id, string Level)> skills, CancellationToken ct);
    Task<IReadOnlyList<AssignmentSnapshot>> AssignmentsAsync(long bookingId, long? userId, CancellationToken ct);
    Task<StaffWorkflowResult> ExecuteAsync(StaffWorkflowCommand command, CancellationToken ct);
}
