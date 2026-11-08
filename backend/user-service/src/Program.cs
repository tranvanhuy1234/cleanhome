using CleanHome.ServiceDefaults;
using CleanHome.User.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCleanHomeService(authentication: true);
builder.Services.AddUserModule();
var app = builder.Build();
app.MapCleanHomeService("user-service", authentication: true);
app.Run();
