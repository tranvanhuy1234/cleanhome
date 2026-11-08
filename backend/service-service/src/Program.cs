using CleanHome.ServiceDefaults;
using CleanHome.Catalog.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCleanHomeService(authentication: true);
builder.Services.AddCatalogModule();
var app = builder.Build();
app.MapCleanHomeService("service-service", authentication: true);
app.Run();
