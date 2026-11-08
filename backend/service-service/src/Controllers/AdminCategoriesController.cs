using CleanHome.Catalog.Api.DTOs.Requests;
using CleanHome.Catalog.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Catalog.Api.Controllers;

[ApiController, Authorize(Roles = "admin"), Route("admin/categories"), RequestSizeLimit(65536)]
public sealed class AdminCategoriesController(ICatalogManagementService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CatalogAdminQuery query, CancellationToken ct) => Ok(await catalog.ListCategoriesAsync(query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await catalog.GetCategoryAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CategoryWriteRequest request, CancellationToken ct)
    {
        var response = await catalog.SaveCategoryAsync(null, request, ct);
        return Created("/api/catalog/admin/categories/" + response.Id, response);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CategoryWriteRequest request, CancellationToken ct) => Ok(await catalog.SaveCategoryAsync(id, request, ct));

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> Status(long id, CatalogStatusRequest request, CancellationToken ct) => Ok(await catalog.SetCategoryStatusAsync(id, request.IsActive!.Value, ct));
}
