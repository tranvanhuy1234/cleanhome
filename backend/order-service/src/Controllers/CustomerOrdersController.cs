using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Order.Api.Controllers;

[ApiController, Authorize(Roles = "customer"), Route("me")]
public sealed class CustomerOrdersController(ICustomerOrderService orders) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderListQuery query, CancellationToken ct) => Ok(await orders.ListAsync(User.UserId(), query, ct));

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] OrderHistoryQuery query, CancellationToken ct) => Ok(await orders.HistoryAsync(User.UserId(), query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await orders.GetAsync(User.UserId(), id, ct));
}
