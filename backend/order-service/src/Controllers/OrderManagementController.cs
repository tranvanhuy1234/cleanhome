using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Order.Api.Controllers;

[ApiController, Authorize(Roles = "admin"), Route("admin/orders"), RequestSizeLimit(65536)]
public sealed class OrderManagementController(IOrderWorkflowService orders) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] AdminOrderQuery query, CancellationToken ct) => Ok(await orders.AdminListAsync(query, ct));
    [HttpGet("{id:long}")] public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await orders.AdminDetailAsync(id, ct));
    [HttpPost("{id:long}/confirm")] public async Task<IActionResult> Confirm(long id, OrderNoteRequest request, CancellationToken ct) => Ok(await orders.ConfirmAsync(id, User.UserId(), request, ct));
    [HttpGet("{id:long}/cancellations")] public async Task<IActionResult> Cancellations(long id, CancellationToken ct) => Ok(await orders.CancellationsAsync(id, null, ct));
    [HttpPost("{id:long}/cancellations/{requestId:long}/reject")]
    public async Task<IActionResult> Reject(long id, long requestId, OrderNoteRequest request, CancellationToken ct) => Ok(await orders.RejectCancellationAsync(id, requestId, User.UserId(), request, ct));
    [HttpPost("{id:long}/cancellations/{requestId:long}/approve")]
    public async Task<IActionResult> Approve(long id, long requestId, ApproveCancellationRequest request, CancellationToken ct) => Operation(await orders.ApproveCancellationAsync(id, requestId, User.UserId(), request, ct));
    [HttpPost("{id:long}/assignments")]
    public async Task<IActionResult> Assign(long id, AssignmentRequest request, CancellationToken ct) => Operation(await orders.AssignAsync(id, User.UserId(), request, ct));
    [HttpPost("{id:long}/unassign")]
    public async Task<IActionResult> Unassign(long id, UnassignRequest request, CancellationToken ct) => Operation(await orders.UnassignAsync(id, User.UserId(), request, ct));
    private IActionResult Operation(OperationResponse result) => WorkflowHttp.Result(this, result);
}

internal static class WorkflowHttp
{
    public static IActionResult Result(ControllerBase controller, OperationResponse result) => result.State switch
    {
        "pending" => controller.Accepted("/api/orders/operations/" + result.Id, result),
        "failed" => controller.Problem(statusCode: result.ErrorStatus ?? 409, title: result.ErrorMessage ?? "Thao tác không được chấp nhận."),
        _ => controller.Ok(result)
    };
}
