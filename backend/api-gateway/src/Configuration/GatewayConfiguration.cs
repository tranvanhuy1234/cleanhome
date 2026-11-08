namespace CleanHome.Gateway.Api.Configuration;

public static class GatewayConfiguration
{
    public static IServiceCollection AddGatewayModule(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));
        return services;
    }
}
