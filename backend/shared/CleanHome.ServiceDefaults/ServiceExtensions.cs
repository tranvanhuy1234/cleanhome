using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication;

namespace CleanHome.ServiceDefaults;

public static class ServiceExtensions
{
    public static WebApplicationBuilder AddCleanHomeService(this WebApplicationBuilder builder, bool database = true, bool authentication = false)
    {
        // Log ra stdout, không yêu cầu quyền ghi Windows Event Log khi chạy local.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        // Environment/CLI phải được ưu tiên hơn file cấu hình local.
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddCommandLine(Environment.GetCommandLineArgs().Skip(1).ToArray());
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddControllers();
        if (authentication)
        {
            builder.Services.AddHttpClient("service-auth", client => client.Timeout = TimeSpan.FromSeconds(5));
            builder.Services.AddScoped<ISessionValidator, RemoteSessionValidator>();
            builder.Services.AddAuthentication("CleanHome")
                .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("CleanHome", _ => { });
            builder.Services.AddAuthorization();
        }
        var checks = builder.Services.AddHealthChecks();
        if (database)
        {
            builder.Services.AddSingleton<DatabaseConnectionFactory>();
            checks.AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        }
        return builder;
    }

    public static WebApplication MapCleanHomeService(this WebApplication app, string serviceName, bool authentication = false)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        if (authentication)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.Use(async (context, next) =>
            {
                if (context.User.Identity?.IsAuthenticated == true) context.Response.Headers.CacheControl = "no-store";
                await next();
            });
        }
        app.MapControllers();
        app.MapGet("/", () => Results.Ok(new { service = serviceName, stage = "foundation" }));
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = async (context, report) =>
            {
                await context.Response.WriteAsJsonAsync(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString()
                    })
                });
            }
        });
        return app;
    }
}
