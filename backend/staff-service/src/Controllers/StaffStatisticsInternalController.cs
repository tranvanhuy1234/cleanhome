using CleanHome.ServiceDefaults;
using CleanHome.Staff.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Staff.Api.Controllers;
[ApiController,AllowAnonymous,Route("internal/statistics")]
public sealed class StaffStatisticsInternalController(IStaffStatisticsRepository statistics,IConfiguration configuration):ControllerBase
{
 [HttpGet("{userId:long}")]
 public async Task<IActionResult> Get(long userId,CancellationToken ct)
 {InternalServiceAccess.Require(Request,configuration);if(userId<=0)return BadRequest();return Ok(await statistics.GetAsync(userId,ct));}
}
