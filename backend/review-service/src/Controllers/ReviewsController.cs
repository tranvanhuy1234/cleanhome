using CleanHome.Review.Api.DTOs.Requests;
using CleanHome.Review.Api.Interfaces;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanHome.Review.Api.Controllers;

[ApiController, Authorize(Roles = "customer"), Route("me"), RequestSizeLimit(16384)]
public sealed class ReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpPost] public async Task<IActionResult> Create(CreateReviewRequest request, CancellationToken ct)
    { var result = await reviews.SubmitAsync(request, User.UserId(), SessionTokens.Bearer(Request)!, ct); return Created("/api/reviews/me/" + result.Id, result); }
    [HttpGet] public async Task<IActionResult> List([FromQuery] ReviewQuery query, CancellationToken ct) => Ok(await reviews.ListAsync(query, User.UserId(), null, ct));
    [HttpGet("{id:long}")] public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await reviews.GetAsync(id, User.UserId(), ct));
}

[ApiController, Authorize(Roles = "admin"), Route("admin/reviews"), RequestSizeLimit(16384)]
public sealed class AdminReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] AdminReviewQuery query, CancellationToken ct) => Ok(await reviews.ListAsync(query, null, query.IsHidden, ct));
    [HttpGet("{id:long}")] public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await reviews.GetAsync(id, null, ct));
    [HttpPatch("{id:long}/visibility")] public async Task<IActionResult> Visibility(long id, ReviewVisibilityRequest request, CancellationToken ct) => Ok(await reviews.SetVisibilityAsync(id, request, User.UserId(), ct));
}

[ApiController, AllowAnonymous, Route("employees")]
public sealed class PublicEmployeeReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpGet("{id:long}")] public async Task<IActionResult> List(long id, [FromQuery] ReviewQuery query, CancellationToken ct) => Ok(await reviews.PublicAsync(id, query, ct));
}
