using CleanHome.Catalog.Api.DTOs.Requests;
using CleanHome.Catalog.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Catalog.Api.Controllers;

[ApiController, AllowAnonymous, Route("services")]
public sealed class ServicesController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Ok(await catalog.GetAsync(id, ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ServiceQueryRequest query, CancellationToken ct) =>
        Ok(await catalog.ListAsync(query, ct));
}
