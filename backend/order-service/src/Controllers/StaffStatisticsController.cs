using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Order.Api.Controllers;
[ApiController,Authorize(Roles="staff"),Route("staff/me/statistics")]
public sealed class StaffStatisticsController(IStaffStatisticsService statistics):ControllerBase
{
 [HttpGet]public async Task<IActionResult> Get([FromQuery]StaffStatisticsQuery query,CancellationToken ct)=>Ok(await statistics.GetAsync(User.UserId(),query,ct));
 [HttpGet("revenue")]public async Task<IActionResult> Revenue([FromQuery]StaffStatisticsQuery query,CancellationToken ct)=>Ok((await statistics.GetAsync(User.UserId(),query,ct)).Revenue);
 [HttpGet("history")]public async Task<IActionResult> History([FromQuery]StaffStatisticsQuery query,CancellationToken ct)=>Ok((await statistics.GetAsync(User.UserId(),query,ct)).History);
}
