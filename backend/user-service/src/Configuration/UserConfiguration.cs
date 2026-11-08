using CleanHome.User.Api.Interfaces;
using CleanHome.User.Api.Repositories;
using CleanHome.User.Api.Services;

namespace CleanHome.User.Api.Configuration;

public static class UserConfiguration
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
