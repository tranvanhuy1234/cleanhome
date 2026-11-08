using CleanHome.ServiceDefaults;
using CleanHome.Review.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCleanHomeService(authentication: true);
builder.Services.AddReviewModule();
var app = builder.Build();
app.MapCleanHomeService("review-service", authentication: true);
app.Run();
