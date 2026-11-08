using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using CleanHome.Staff.Api.Interfaces;
using CleanHome.Staff.Api.DTOs.Requests;
using CleanHome.Staff.Api.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Staff.Api.Controllers;
[ApiController,Authorize(Roles="staff"),Route("me/dispatch"),RequestSizeLimit(16384)]
public sealed class StaffDispatchController(IDispatchRepository dispatch,IStaffService staff):ControllerBase
{
 [HttpGet("presence")]public async Task<IActionResult> Presence(CancellationToken ct)=>Ok(await dispatch.PresenceAsync(User.UserId(),ct));
 [HttpPut("presence")]public async Task<IActionResult> Presence(PresenceRequest request,CancellationToken ct)=>Ok(await dispatch.SavePresenceAsync(User.UserId(),request,ct));
 [HttpGet("offers")]public async Task<IActionResult> Offers(CancellationToken ct)=>Ok(await dispatch.OffersAsync(User.UserId(),ct));
 [HttpGet("offers/{id:long}")]public async Task<IActionResult> Offer(long id,CancellationToken ct)=>Ok(await dispatch.OfferAsync(User.UserId(),id,ct));
 [HttpPost("offers/{id:long}/skip")]public async Task<IActionResult> Skip(long id,CancellationToken ct){await dispatch.SkipAsync(User.UserId(),id,ct);return NoContent();}
 [HttpPost("jobs/{id:long}/arrive")]public async Task<IActionResult> Arrive(long id,CancellationToken ct){await dispatch.ArriveAsync(User.UserId(),id,ct);return NoContent();}
 [HttpPost("jobs/{id:long}/support")]public async Task<IActionResult> Support(long id,SupportRequest request,CancellationToken ct){await dispatch.SupportAsync(User.UserId(),id,request.Reason,ct);return NoContent();}
 [HttpGet("events")]public async Task Events([FromServices]ISessionValidator sessions,CancellationToken ct)
 {
  Response.ContentType="text/event-stream";Response.Headers.CacheControl="no-store";Response.Headers["X-Accel-Buffering"]="no";
  var token=SessionTokens.Bearer(Request)!;string? previous=null,previousJobs=null;var nextAuth=DateTime.MinValue;
  // Short-lived streams reconnect with a fresh session check. No token in URLs.
  var end=DateTime.UtcNow.AddSeconds(45);
  while(!ct.IsCancellationRequested&&DateTime.UtcNow<end){
   if(DateTime.UtcNow>=nextAuth){var identity=await sessions.ValidateAsync(token,ct);if(identity is null||identity.Role!="staff")break;nextAuth=DateTime.UtcNow.AddSeconds(10);}
   var offers=await dispatch.OffersAsync(User.UserId(),ct);var body=JsonSerializer.Serialize(offers,new JsonSerializerOptions(JsonSerializerDefaults.Web));
   var revision=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
   if(previous!=revision){await Response.WriteAsync("data: "+body+"\n\n",ct);previous=revision;}else await Response.WriteAsync(": keepalive\n\n",ct);
   var jobs=await staff.AssignmentsAsync(0,User.UserId(),ct);var jobRevision=JsonSerializer.Serialize(jobs.Select(j=>new{j.Id,j.Status,j.Version}));
   if(previousJobs!=jobRevision){await Response.WriteAsync("event: jobs\ndata: {}\n\n",ct);previousJobs=jobRevision;}
   await Response.Body.FlushAsync(ct);await Task.Delay(1500,ct);
  }
 }
}
[ApiController,AllowAnonymous,Route("internal/dispatch"),RequestSizeLimit(65536)]
public sealed class InternalDispatchController(IDispatchRepository dispatch,IConfiguration configuration):ControllerBase
{
 [HttpPost]public async Task<IActionResult> Register(DispatchRegistration request,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);await dispatch.RegisterDispatchAsync(request,ct);return NoContent();}
 [HttpGet("{bookingId:long}")]public async Task<IActionResult> Status(long bookingId,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);return Ok(await dispatch.DispatchStatusAsync(bookingId,ct));}
 [HttpGet("offers/{id:long}/{userId:long}")]public async Task<IActionResult> Offer(long id,long userId,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);return Ok(await dispatch.OfferAsync(userId,id,ct));}
}
[ApiController,Authorize(Roles="admin"),Route("admin/dispatch"),RequestSizeLimit(16384)]
public sealed class AdminDispatchController(IDispatchRepository dispatch):ControllerBase
{
 [HttpGet("settings")]public async Task<IActionResult> Settings(CancellationToken ct)=>Ok(await dispatch.SettingsAsync(ct));
 [HttpPut("settings")]public async Task<IActionResult> Settings(DispatchSettings request,CancellationToken ct){await dispatch.SaveSettingsAsync(request,ct);return NoContent();}
 [HttpGet("jobs")]public async Task<IActionResult> Jobs(CancellationToken ct)=>Ok(await dispatch.DispatchListAsync(ct));
 [HttpGet("ready")]public async Task<IActionResult> Ready(CancellationToken ct)=>Ok(await dispatch.ReadyEmployeesAsync(ct));
 [HttpGet("jobs/{id:long}/audit")]public async Task<IActionResult> Audit(long id,CancellationToken ct)=>Ok(await dispatch.DispatchAuditAsync(id,ct));
 [HttpPost("jobs/{id:long}/{operation}")]public async Task<IActionResult> Control(long id,string operation,CancellationToken ct){await dispatch.ControlDispatchAsync(id,operation,ct);return NoContent();}
 [HttpPut("services/{id:long}/skills")]public async Task<IActionResult> Skills(long id,long[] skills,CancellationToken ct){await dispatch.SaveServiceSkillsAsync(id,skills,ct);return NoContent();}
 [HttpGet("services/{id:long}/skills")]public async Task<IActionResult> Skills(long id,CancellationToken ct)=>Ok(await dispatch.ServiceSkillsAsync(id,ct));
 [HttpGet("support")]public async Task<IActionResult> Support(CancellationToken ct)=>Ok(await dispatch.SupportListAsync(ct));
 [HttpPost("support/{id:long}/resolve")]public async Task<IActionResult> Resolve(long id,CancellationToken ct){await dispatch.ResolveSupportAsync(id,ct);return NoContent();}
}
public sealed class SupportRequest{[System.ComponentModel.DataAnnotations.Required,System.ComponentModel.DataAnnotations.StringLength(2000,MinimumLength=3)]public string Reason{get;set;}="";}
