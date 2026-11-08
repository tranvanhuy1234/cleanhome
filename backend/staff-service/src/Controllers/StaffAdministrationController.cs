using CleanHome.ServiceDefaults;
using CleanHome.Staff.Api.DTOs.Requests;
using CleanHome.Staff.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Staff.Api.Controllers;

[ApiController, Authorize(Roles = "admin"), Route("admin"), RequestSizeLimit(65536)]
public sealed class StaffAdministrationController(IStaffService staff) : ControllerBase
{
    [HttpGet("branches")] public async Task<IActionResult> Branches([FromQuery] StaffPageQuery query, CancellationToken ct) => Ok(await staff.BranchesAsync(query, ct));
    [HttpGet("branches/{id:long}")] public async Task<IActionResult> Branch(long id, CancellationToken ct) => Ok(await staff.BranchAsync(id, ct));
    [HttpPost("branches")] public async Task<IActionResult> CreateBranch(BranchRequest input, CancellationToken ct)
    { var result = await staff.SaveBranchAsync(null, input, ct); return Created("/api/staff/admin/branches/" + result.Id, result); }
    [HttpPut("branches/{id:long}")] public async Task<IActionResult> UpdateBranch(long id, BranchRequest input, CancellationToken ct) => Ok(await staff.SaveBranchAsync(id, input, ct));
    [HttpGet("employees")] public async Task<IActionResult> Employees([FromQuery] StaffPageQuery query, CancellationToken ct) => Ok(await staff.EmployeesAsync(query, ct));
    [HttpGet("employees/{id:long}")] public async Task<IActionResult> Employee(long id, CancellationToken ct) => Ok(await staff.EmployeeAsync(id, ct));
    [HttpPost("employees")] public async Task<IActionResult> CreateEmployee(EmployeeCreateRequest input, CancellationToken ct)
    { var result = await staff.CreateEmployeeAsync(input, SessionTokens.Bearer(Request)!, ct); return Created("/api/staff/admin/employees/" + result.Id, result); }
    [HttpPut("employees/{id:long}")] public async Task<IActionResult> UpdateEmployee(long id, EmployeeUpdateRequest input, CancellationToken ct) => Ok(await staff.UpdateEmployeeAsync(id, input, ct));
    [HttpGet("employees/{employeeId:long}/availability")] public async Task<IActionResult> Availability(long employeeId, CancellationToken ct) => Ok(await staff.AvailabilityAsync(employeeId, ct));
    [HttpPost("employees/{employeeId:long}/availability")] public async Task<IActionResult> CreateAvailability(long employeeId, AvailabilityRequest input, CancellationToken ct)
    { var result = await staff.SaveAvailabilityAsync(employeeId, null, input, ct); return Created("/api/staff/admin/employees/" + employeeId + "/availability/" + result.Id, result); }
    [HttpPut("employees/{employeeId:long}/availability/{id:long}")] public async Task<IActionResult> UpdateAvailability(long employeeId, long id, AvailabilityRequest input, CancellationToken ct) => Ok(await staff.SaveAvailabilityAsync(employeeId, id, input, ct));
    [HttpDelete("employees/{employeeId:long}/availability/{id:long}")] public async Task<IActionResult> DeleteAvailability(long employeeId, long id, CancellationToken ct)
    { await staff.DeleteAvailabilityAsync(employeeId, id, ct); return NoContent(); }
    [HttpGet("skills")] public async Task<IActionResult> Skills(CancellationToken ct) => Ok(await staff.SkillsAsync(ct));
    [HttpPost("skills")] public async Task<IActionResult> CreateSkill(SkillRequest input, CancellationToken ct)
    { var result = await staff.SaveSkillAsync(null, input, ct); return Created("/api/staff/admin/skills/" + result.Id, result); }
    [HttpPut("skills/{id:long}")] public async Task<IActionResult> UpdateSkill(long id, SkillRequest input, CancellationToken ct) => Ok(await staff.SaveSkillAsync(id, input, ct));
    [HttpGet("employees/{id:long}/skills")] public async Task<IActionResult> EmployeeSkills(long id, CancellationToken ct) => Ok(await staff.EmployeeSkillsAsync(id, ct));
    [HttpPut("employees/{id:long}/skills")] public async Task<IActionResult> SetSkills(long id, EmployeeSkillsRequest input, CancellationToken ct)
    { await staff.SetSkillsAsync(id, input, ct); return NoContent(); }
    [HttpGet("bookings/{id:long}/assignments")] public async Task<IActionResult> Assignments(long id, CancellationToken ct) => Ok(await staff.AssignmentsAsync(id, null, ct));
}
