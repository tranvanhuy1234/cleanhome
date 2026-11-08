using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Order.Api.Controllers;
[ApiController,Authorize(Roles="staff"),Route("staff/orders")]
public sealed class StaffDestinationController(IStaffDestinationService destinations):ControllerBase
{
 [HttpGet("{id:long}/summary")]
 public async Task<IActionResult> Summary(long id,CancellationToken ct)=>Ok(await destinations.SummaryAsync(id,User.UserId(),ct));
 [HttpGet("{id:long}/destination")]
 public async Task<IActionResult> Destination(long id,CancellationToken ct)=>Ok(await destinations.GetAsync(id,User.UserId(),ct));
}
