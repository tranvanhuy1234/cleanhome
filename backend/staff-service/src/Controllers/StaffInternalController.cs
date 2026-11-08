using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using CleanHome.Staff.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Staff.Api.Controllers;

[ApiController, AllowAnonymous, Route("internal"), RequestSizeLimit(65536)]
public sealed class StaffInternalController(IStaffService staff, IConfiguration configuration) : ControllerBase
{
    [HttpGet("branches")] public async Task<IActionResult> Branches([FromQuery] CleanHome.Staff.Api.DTOs.Requests.StaffPageQuery query,CancellationToken ct)
    { InternalServiceAccess.Require(Request,configuration);return Ok(await staff.BranchesAsync(query,ct)); }
    [HttpPost("operations")] public async Task<IActionResult> Execute(StaffWorkflowCommand command, CancellationToken ct)
    { InternalServiceAccess.Require(Request, configuration); return Ok(await staff.ExecuteAsync(command, ct)); }
    [HttpGet("bookings/{id:long}/assignments")] public async Task<IActionResult> Assignments(long id, CancellationToken ct)
    { InternalServiceAccess.Require(Request, configuration); if (id <= 0) return BadRequest(); return Ok(await staff.AssignmentsAsync(id, null, ct)); }
}
