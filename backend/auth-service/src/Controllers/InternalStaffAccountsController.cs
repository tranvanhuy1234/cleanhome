using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Auth.Api.Controllers;
[ApiController,AllowAnonymous,Route("internal/active-staff")]
public sealed class InternalStaffAccountsController(CleanHome.Auth.Api.Interfaces.IActiveStaffRepository accounts,IConfiguration configuration):ControllerBase
{
 [HttpPost]public async Task<IActionResult> Get(long[] ids,CancellationToken ct)
 {
  InternalServiceAccess.Require(Request,configuration);if(ids.Length>1000||ids.Any(id=>id<=0))return BadRequest();
  return Ok(await accounts.GetAsync(ids,ct));
 }
}
