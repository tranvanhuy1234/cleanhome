using CleanHome.ServiceDefaults;
using CleanHome.Staff.Api.Repositories;
using CleanHome.Staff.Api.DTOs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Staff.Api.Controllers;
[ApiController,Authorize,RequestSizeLimit(4096)]
public sealed class AssignmentLifecycleController(AssignmentLifecycleRepository repository):ControllerBase{
 [HttpGet("me/withdrawals"),Authorize(Roles="staff")]public async Task<IActionResult> Own([FromQuery]long? bookingId,CancellationToken ct)=>Ok(new{requests=await repository.Requests(User.UserId(),bookingId,null,ct),history=await repository.History(User.UserId(),bookingId,ct)});
 [HttpGet("admin/withdrawals"),Authorize(Roles="admin")]public async Task<IActionResult> Admin([FromQuery]long? bookingId,CancellationToken ct)=>Ok(new{requests=await repository.Requests(null,bookingId,null,ct),history=await repository.History(null,bookingId,ct)});
 [HttpGet("admin/withdrawal-policy"),Authorize(Roles="admin")]public async Task<IActionResult> Policy(CancellationToken ct)=>Ok(new{earlyMinutes=await repository.Policy(ct)});
 [HttpPut("admin/withdrawal-policy"),Authorize(Roles="admin")]public async Task<IActionResult> Policy(WithdrawalPolicy request,CancellationToken ct){await repository.SavePolicy(request.EarlyMinutes,ct);return NoContent();}
}
