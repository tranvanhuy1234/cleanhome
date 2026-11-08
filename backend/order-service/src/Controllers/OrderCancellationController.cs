using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Order.Api.Controllers;

[ApiController, Authorize(Roles = "customer"), Route("me/{id:long}"), RequestSizeLimit(16384)]
public sealed class OrderCancellationController(IOrderWorkflowService orders) : ControllerBase
{
    [HttpPost("cancellations")] public async Task<IActionResult> CreateRequest(long id, CancellationRequest request, CancellationToken ct)
    { var result = await orders.RequestCancellationAsync(id, User.UserId(), request, ct); return Created("/api/orders/me/" + id + "/cancellations", result); }
    [HttpGet("cancellations")] public async Task<IActionResult> List(long id, CancellationToken ct) => Ok(await orders.CancellationsAsync(id, User.UserId(), ct));
    [HttpGet("assignments")] public async Task<IActionResult> Assignments(long id, CancellationToken ct) => Ok(await orders.CustomerAssignmentsAsync(id, User.UserId(), ct));
}
