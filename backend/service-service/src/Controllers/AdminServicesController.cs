using CleanHome.Catalog.Api.DTOs.Requests;
using CleanHome.Catalog.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Catalog.Api.Controllers;

[ApiController, Authorize(Roles = "admin"), Route("admin/services"), RequestSizeLimit(65536)]
public sealed class AdminServicesController(ICatalogManagementService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminServiceQuery query, CancellationToken ct) => Ok(await catalog.ListServicesAsync(query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await catalog.GetServiceAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(ServiceWriteRequest request, CancellationToken ct)
    {
        var response = await catalog.SaveServiceAsync(null, request, ct);
        return Created("/api/catalog/admin/services/" + response.Id, response);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, ServiceWriteRequest request, CancellationToken ct) => Ok(await catalog.SaveServiceAsync(id, request, ct));

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> Status(long id, CatalogStatusRequest request, CancellationToken ct) => Ok(await catalog.SetServiceStatusAsync(id, request.IsActive!.Value, ct));
}
