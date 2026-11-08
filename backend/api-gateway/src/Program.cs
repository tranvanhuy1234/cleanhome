using CleanHome.ServiceDefaults;
using CleanHome.Gateway.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCleanHomeService(database: false);
builder.Services.AddGatewayModule(builder.Configuration);
var app = builder.Build();
app.UseCors();
app.MapCleanHomeService("api-gateway");
app.MapMethods("/api/{service}/internal/{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"],
    () => Results.NotFound()).WithOrder(-100);
app.MapReverseProxy();
app.Run();
