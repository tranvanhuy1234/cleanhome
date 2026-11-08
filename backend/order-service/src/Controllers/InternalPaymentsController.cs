using CleanHome.Order.Api.Repositories;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Order.Api.Controllers;
[ApiController,AllowAnonymous,Route("internal/payments")]
public sealed class InternalPaymentsController(OrderPaymentRepository repository,IStaffWorkflowClient staff,IConfiguration configuration):ControllerBase
{
 [HttpGet("{id:long}")]public async Task<IActionResult> Fact(long id,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);return Ok(await repository.FactAsync(id,ct));}
 [HttpGet("{id:long}/assignments")]public async Task<IActionResult> Assignments(long id,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);return Ok(await staff.AssignmentsAsync(id,ct));}
 [HttpPost("{id:long}")]public async Task<IActionResult> Mirror(long id,PaymentMirror body,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);await repository.MirrorAsync(id,body,ct);return NoContent();}
}
