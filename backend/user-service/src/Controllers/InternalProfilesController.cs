using CleanHome.ServiceDefaults;
using CleanHome.User.Api.DTOs.Requests;
using CleanHome.User.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.User.Api.Controllers;

[ApiController, AllowAnonymous, Route("internal/profiles"), RequestSizeLimit(16384)]
public sealed class InternalProfilesController(IProfileService profiles, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Provision(ProvisionProfileRequest input, CancellationToken ct)
    {
        InternalServiceAccess.Require(Request, configuration);
        await profiles.ProvisionAsync(input, ct);
        return NoContent();
    }
}
