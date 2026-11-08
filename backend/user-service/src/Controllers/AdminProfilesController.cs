using CleanHome.User.Api.DTOs.Requests;
using CleanHome.User.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.User.Api.Controllers;

[ApiController, Authorize(Roles = "admin"), Route("profiles"), RequestSizeLimit(16384)]
public sealed class AdminProfilesController(IProfileService profiles) : ControllerBase
{
    [HttpGet("{userId:long}")]
    public async Task<IActionResult> Get(long userId, CancellationToken ct) =>
        Ok(await profiles.GetByAdminAsync(userId, ct));

    [HttpPut("{userId:long}")]
    public async Task<IActionResult> Update(long userId, AdminProfileRequest request, CancellationToken ct) =>
        Ok(await profiles.UpdateByAdminAsync(userId, request, ct));
}
