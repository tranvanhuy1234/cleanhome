using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Order.Api.Controllers;

[ApiController, Route("staff/orders"), Authorize(Roles = "staff"), RequestSizeLimit(16384)]
public sealed class OrderProgressController(IOrderWorkflowService orders) : ControllerBase
{
    [HttpPost("{id:long}/progress")] public async Task<IActionResult> Progress(long id, JobProgressRequest request, CancellationToken ct) =>
        WorkflowHttp.Result(this, await orders.ProgressAsync(id, User.UserId(), request, ct));
}

[ApiController, Route("operations"), Authorize]
public sealed class OrderOperationsController(IOrderWorkflowService orders) : ControllerBase
{
    [HttpGet("{id}")] public async Task<IActionResult> Get(string id, CancellationToken ct) => Ok(await orders.OperationAsync(id, User.UserId(), User.IsInRole("admin"), ct));
}
