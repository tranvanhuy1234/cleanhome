using CleanHome.Auth.Api.Configuration;
using CleanHome.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddCleanHomeService(authentication: true);
builder.Services.AddAuthModule(builder.Configuration);
var app = builder.Build();
app.MapCleanHomeService("auth-service", authentication: true);
app.UseRateLimiter();
app.Run();
