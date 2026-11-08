using System.Text.Json.Serialization;
using CleanHome.Payment.Api.Configuration;
using CleanHome.Payment.Api.DTOs;
using CleanHome.Payment.Api.Interfaces;
using CleanHome.Payment.Api.Services;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Payment.Api.Controllers;
[ApiController,Authorize,RequestSizeLimit(16384)]
public sealed class PaymentsController(PaymentService service,IPaymentRepository repository,VnPayOptions options):ControllerBase{
 [HttpPost("vnpay/create"),HttpPost("vnpay/retry"),Authorize(Roles="customer")]
 public async Task<IActionResult> Create(CheckoutRequest request,CancellationToken ct)=>Ok(await service.Create(request,User.UserId(),HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString()??"127.0.0.1",ct));
 [HttpGet("orders/{id:long}/status")]
 public async Task<IActionResult> Status(long id,CancellationToken ct)=>Ok(await service.Status(id,User.UserId(),User.IsInRole("admin")?"admin":User.IsInRole("staff")?"staff":"customer",ct));
 [HttpPost("cash/confirm-collection"),Authorize(Roles="staff")]
 public async Task<IActionResult> Collect(CashRequest request,CancellationToken ct){await service.Collect(request.OrderId,User.UserId(),ct);return NoContent();}
 [HttpPost("cash/customer-confirm"),Authorize(Roles="customer")]
 public async Task<IActionResult> Confirm(CashRequest request,CancellationToken ct){await service.Customer(request,User.UserId(),ct);return NoContent();}
 [HttpGet("vnpay/ipn"),AllowAnonymous]
 public async Task<IActionResult> Ipn(CancellationToken ct){Response.Headers.CacheControl="no-store";try{var code=await service.Ipn(Request.Query,ct);return Ok(new IpnResponse(code,code=="00"?"Confirm Success":code=="02"?"Already confirmed":code=="97"?"Invalid signature":code=="04"?"Invalid amount":code=="01"?"Order not found":"Retry later"));}catch(Exception)when(!ct.IsCancellationRequested){return Ok(new IpnResponse("99","Retry later"));}}
 [HttpGet("vnpay/return"),AllowAnonymous]
 public async Task<IActionResult> Return(CancellationToken ct){Response.Headers.CacheControl="no-store";var id=await service.Return(Request.Query,ct);if(id is null)return BadRequest(new{message="Kết quả thanh toán không hợp lệ."});return Redirect(options.Frontend.TrimEnd('/')+"/don/"+id+"?payment=verifying");}
 [HttpGet("admin/payments"),Authorize(Roles="admin")]
 public async Task<IActionResult> AdminList([FromQuery]int page=1,[FromQuery]string? status=null,CancellationToken ct=default){if(page is <1 or >100000||status?.Length>32)throw new ApiException(400,"Bộ lọc không hợp lệ.");return Ok(await repository.AdminList(page,status,ct));}
 [HttpGet("admin/payments/{id:long}"),Authorize(Roles="admin")]
 public async Task<IActionResult> Audit(long id,CancellationToken ct)=>Ok(new{payment=await repository.Read(id,ct),history=await repository.Audit(id,ct)});
 [HttpPost("admin/payments/{id:long}/{operation}"),Authorize(Roles="admin")]
 public async Task<IActionResult> Admin(long id,string operation,AdminPaymentRequest request,CancellationToken ct){await service.Admin(id,operation,request,User.UserId(),ct);return NoContent();}
 [HttpPost("admin/transactions/{reference}/reconcile"),Authorize(Roles="admin")]
 public async Task<IActionResult> Reconcile(string reference,CancellationToken ct){await service.Reconcile(reference,ct);return NoContent();}
 [HttpPost("admin/payments/{id:long}/refunds/{caseId:long}/confirm"),Authorize(Roles="admin")]
 public async Task<IActionResult> Refund(long id,long caseId,AdminPaymentRequest request,CancellationToken ct){await service.Refund(id,caseId,User.UserId(),request.Evidence,ct);return NoContent();}
 private sealed record IpnResponse([property:JsonPropertyName("RspCode")]string RspCode,[property:JsonPropertyName("Message")]string Message);
}
[ApiController,AllowAnonymous,Route("internal/orders")]
public sealed class InternalPaymentController(PaymentService service,IPaymentRepository repository,IConfiguration configuration):ControllerBase{
 [HttpGet("{id:long}/archive-safe")]public async Task<IActionResult> ArchiveSafe(long id,[FromQuery]bool managed,[FromQuery]bool cancelled,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);return Ok(await repository.ArchiveSafe(id,managed,cancelled,ct));}
 [HttpPost("{id:long}")]public async Task<IActionResult> Enroll(long id,CancellationToken ct){InternalServiceAccess.Require(Request,configuration);await service.Enroll(id,ct);return NoContent();}
}
