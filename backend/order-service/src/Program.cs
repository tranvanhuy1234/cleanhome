using CleanHome.ServiceDefaults;
using CleanHome.Order.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCleanHomeService(authentication: true);
builder.Services.AddOrderModule();
var app = builder.Build();
app.MapCleanHomeService("order-service", authentication: true);
app.Run();
