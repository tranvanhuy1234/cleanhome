using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CleanHome.Order.Api.Controllers;
[ApiController,Authorize(Roles="customer"),Route("booking"),RequestSizeLimit(16384)]
public sealed class BookingCreationController(IBookingCreationService bookings):ControllerBase
{
 [HttpGet("branches")] public async Task<IActionResult> Branches(CancellationToken ct)=>Ok(await bookings.BranchesAsync(ct));
 [HttpPost] public async Task<IActionResult> Create(CreateBookingRequest request,CancellationToken ct)
 {var result=await bookings.CreateAsync(User.UserId(),SessionTokens.Bearer(Request)!,request,ct);return Created("/api/orders/me/"+result.Id,result);}
}
