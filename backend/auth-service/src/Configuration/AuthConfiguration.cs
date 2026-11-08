using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using CleanHome.Auth.Api.Interfaces;
using CleanHome.Auth.Api.Repositories;
using CleanHome.Auth.Api.Services;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.RateLimiting;

namespace CleanHome.Auth.Api.Configuration;

public sealed class AuthOptions
{
    [Range(1, 168)] public int SessionHours { get; set; } = 8;
    [Range(3, 20)] public int LockoutThreshold { get; set; } = 5;
    [Range(1, 60)] public int LockoutMinutes { get; set; } = 15;
    [Range(1, 1000)] public int RequestsPerMinute { get; set; } = 20;
}

public sealed class ProfileDeliveryOptions
{
    [Required, Url] public string UserUrl { get; set; } = "http://localhost:5002";
    public string InternalKey { get; set; } = "";
}

public static class AuthConfiguration
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IActiveStaffRepository,ActiveStaffRepository>();
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection("Authentication")).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ProfileDeliveryOptions>().Configure(options =>
        {
            options.UserUrl = configuration["Services:UserUrl"] ?? "http://localhost:5002";
            options.InternalKey = configuration["InternalApi:Key"] ?? "";
        }).ValidateDataAnnotations();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IProfileOutboxRepository, ProfileOutboxRepository>();
        services.AddScoped<IProfileDelivery, ProfileDelivery>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionValidator>(provider => provider.GetRequiredService<IAuthService>());
        services.AddHttpClient("profile-delivery", client => client.Timeout = TimeSpan.FromSeconds(3));
        services.AddHostedService<ProfileOutboxWorker>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("credentials", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("Authentication:RequestsPerMinute", 20),
                    Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
        });
        return services;
    }
}
