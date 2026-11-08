using CleanHome.ServiceDefaults;
using CleanHome.Staff.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCleanHomeService(authentication: true);
builder.Services.AddStaffModule();
var app = builder.Build();
app.MapCleanHomeService("staff-service", authentication: true);
app.Run();
