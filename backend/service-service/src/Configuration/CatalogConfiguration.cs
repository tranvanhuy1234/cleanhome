using CleanHome.Catalog.Api.Interfaces;
using CleanHome.Catalog.Api.Repositories;
using CleanHome.Catalog.Api.Services;

namespace CleanHome.Catalog.Api.Configuration;

public static class CatalogConfiguration
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ICatalogManagementRepository, CatalogManagementRepository>();
        services.AddScoped<ICatalogManagementService, CatalogManagementService>();
        return services;
    }
}
