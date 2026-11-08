using CleanHome.ServiceDefaults;
using CleanHome.Staff.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Staff.Api.Controllers;

[ApiController, Authorize(Roles = "staff"), Route("me")]
public sealed class StaffWorkController(IStaffService staff) : ControllerBase
{
    [HttpGet("profile")] public async Task<IActionResult> Profile(CancellationToken ct) => Ok(await staff.MyEmployeeAsync(User.UserId(), ct));
    [HttpGet("jobs")] public async Task<IActionResult> Jobs(CancellationToken ct) => Ok(await staff.AssignmentsAsync(0, User.UserId(), ct));
    [HttpGet("availability")] public async Task<IActionResult> Availability(CancellationToken ct)
    { var employee = await staff.MyEmployeeAsync(User.UserId(), ct); return Ok(await staff.AvailabilityAsync(employee.Id, ct)); }
}
