using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Order.Api.Controllers;
[ApiController,Authorize,Route("dispatch"),RequestSizeLimit(16384)]
public sealed class DispatchController(IDispatchBridge dispatch,IOrderWorkflowService orders,ICustomerOrderRepository bookings):ControllerBase
{
 [HttpPost("offers/{id:long}/accept"),Authorize(Roles="staff")]
 public async Task<IActionResult> Accept(long id,WorkflowRequest request,CancellationToken ct){var offer=await dispatch.OfferAsync(User.UserId(),id,ct);return WorkflowHttp.Result(this,await orders.ClaimAsync(offer.BookingId,id,User.UserId(),request,ct));}
 [HttpGet("bookings/{id:long}"),Authorize(Roles="customer,admin")]
 public async Task<IActionResult> Status(long id,CancellationToken ct){var booking=await bookings.FindAsync(User.IsInRole("admin")?null:User.UserId(),id,ct);if(booking is null)throw new ApiException(404,"Không tìm thấy đơn.");if(booking.Summary.Status=="pending_payment")return Ok(new{bookingId=id,state="awaiting_payment",requiredStaff=0,filled=0,accepted=0,round=0,offerUntil=(DateTime?)null,searchUntil=(DateTime?)null});return Ok(await dispatch.StatusAsync(id,ct));}
}
