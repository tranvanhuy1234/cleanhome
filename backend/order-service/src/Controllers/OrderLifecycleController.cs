using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Repositories;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Order.Api.Controllers;
[ApiController,Authorize,RequestSizeLimit(16384)]
public sealed class OrderLifecycleController(IOrderWorkflowService workflows,OrderArchiveRepository archives):ControllerBase{
 [HttpPost("staff/orders/{id:long}/withdraw"),Authorize(Roles="staff")]
 public async Task<IActionResult> Withdraw(long id,LifecycleRequest request,CancellationToken ct)=>WorkflowHttp.Result(this,await workflows.LifecycleAsync(id,User.UserId(),"withdraw",request,null,ct));
 [HttpPost("admin/orders/{id:long}/withdrawals/{requestId:long}/review"),Authorize(Roles="admin")]
 public async Task<IActionResult> Review(long id,long requestId,LifecycleRequest request,CancellationToken ct)=>WorkflowHttp.Result(this,await workflows.LifecycleAsync(id,User.UserId(),request.Approve?"withdraw_approve":"withdraw_reject",request,requestId,ct));
 [HttpPost("admin/orders/{id:long}/cancel"),Authorize(Roles="admin")]
 public async Task<IActionResult> Cancel(long id,LifecycleRequest request,CancellationToken ct)=>WorkflowHttp.Result(this,await workflows.LifecycleAsync(id,User.UserId(),"admin_cancel",request,null,ct));
 [HttpPost("admin/orders/{id:long}/stop"),Authorize(Roles="admin")]
 public async Task<IActionResult> Stop(long id,LifecycleRequest request,CancellationToken ct)=>WorkflowHttp.Result(this,await workflows.LifecycleAsync(id,User.UserId(),"stop",request,null,ct));
 [HttpGet("admin/orders/{id:long}/archive"),Authorize(Roles="admin")]
 public async Task<IActionResult> Archive(long id,CancellationToken ct)=>Ok(await archives.Metadata(id,ct));
 [HttpPost("admin/orders/{id:long}/archive"),Authorize(Roles="admin")]
 public async Task<IActionResult> Archive(long id,ArchiveRequest request,CancellationToken ct)=>Ok(await archives.Change(id,User.UserId(),true,request,ct));
 [HttpPost("admin/orders/{id:long}/restore"),Authorize(Roles="admin")]
 public async Task<IActionResult> Restore(long id,ArchiveRequest request,CancellationToken ct)=>Ok(await archives.Change(id,User.UserId(),false,request,ct));
}
