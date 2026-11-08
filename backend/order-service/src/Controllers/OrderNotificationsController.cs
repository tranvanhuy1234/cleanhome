using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using CleanHome.Order.Api.Repositories;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Order.Api.Controllers;
[ApiController,Authorize(Roles="customer,admin"),Route("notifications")]
public sealed class OrderNotificationsController(OrderNotificationRepository repository):ControllerBase{
 [HttpGet]public async Task<IActionResult> Feed(CancellationToken ct)=>Ok(await repository.Feed(User.IsInRole("admin")?null:User.UserId(),ct));
 [HttpGet("events")]public async Task Events([FromServices]ISessionValidator sessions,CancellationToken ct){Response.ContentType="text/event-stream";Response.Headers.CacheControl="no-store";Response.Headers["X-Accel-Buffering"]="no";var token=SessionTokens.Bearer(Request)!;var until=DateTime.UtcNow.AddSeconds(45);var nextCheck=DateTime.UtcNow;string? last=null;while(!ct.IsCancellationRequested&&DateTime.UtcNow<until){if(DateTime.UtcNow>=nextCheck){var identity=await sessions.ValidateAsync(token,ct);if(identity is null||identity.Id!=User.UserId()||identity.Role!=(User.IsInRole("admin")?"admin":"customer"))break;nextCheck=DateTime.UtcNow.AddSeconds(10);}var json=JsonSerializer.Serialize(await repository.Feed(User.IsInRole("admin")?null:User.UserId(),ct));var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));if(hash!=last){await Response.WriteAsync("data: "+json+"\n\n",ct);last=hash;}else await Response.WriteAsync(": keepalive\n\n",ct);await Response.Body.FlushAsync(ct);await Task.Delay(1500,ct);}}
}
