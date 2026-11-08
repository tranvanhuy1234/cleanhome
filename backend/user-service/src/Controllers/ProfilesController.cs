using CleanHome.ServiceDefaults;
using CleanHome.User.Api.DTOs.Requests;
using CleanHome.User.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.User.Api.Controllers;

[ApiController, Authorize, Route("me"), RequestSizeLimit(16384)]
public sealed class ProfilesController(IProfileService profiles) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await profiles.GetAsync(User.UserId(), ct));

    [HttpPut]
    public async Task<IActionResult> Save(ProfileRequest input, CancellationToken ct) => Ok(await profiles.SaveAsync(User.UserId(), input, ct));

    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses(CancellationToken ct) => Ok(await profiles.AddressesAsync(User.UserId(), ct));

    [HttpGet("addresses/{id:long}")]
    public async Task<IActionResult> Address(long id, CancellationToken ct) => Ok(await profiles.AddressAsync(User.UserId(), id, ct));

    [HttpPost("addresses")]
    public async Task<IActionResult> CreateAddress(AddressRequest input, CancellationToken ct)
    {
        var address = await profiles.AddAddressAsync(User.UserId(), input, ct);
        return Created("/api/users/me/addresses/" + address.Id, address);
    }

    [HttpPut("addresses/{id:long}")]
    public async Task<IActionResult> UpdateAddress(long id, AddressRequest input, CancellationToken ct)
    {
        await profiles.UpdateAddressAsync(User.UserId(), id, input, ct);
        return NoContent();
    }

    [HttpDelete("addresses/{id:long}")]
    public async Task<IActionResult> DeleteAddress(long id, CancellationToken ct)
    {
        await profiles.DeleteAddressAsync(User.UserId(), id, ct);
        return NoContent();
    }
}
