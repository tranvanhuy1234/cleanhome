using CleanHome.Staff.Api.Interfaces;
using CleanHome.Staff.Api.Repositories;
using CleanHome.Staff.Api.Services;

namespace CleanHome.Staff.Api.Configuration;

public static class StaffConfiguration
{
    public static IServiceCollection AddStaffModule(this IServiceCollection services)
    {
        services.AddScoped<AssignmentLifecycleRepository>();
        services.AddScoped<IDispatchRepository>(provider=>provider.GetRequiredService<StaffRepository>());
        services.AddScoped<StaffRepository>();
        services.AddScoped<IActiveStaffClient,ActiveStaffClient>();
        services.AddHostedService<DispatchWorker>();
        services.AddScoped<IStaffStatisticsRepository,StaffStatisticsRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>(); services.AddScoped<IStaffService, StaffService>();
        services.AddHttpClient("staff-account", client => client.Timeout = TimeSpan.FromSeconds(5)); return services;
    }
}
