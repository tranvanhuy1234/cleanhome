using CleanHome.Review.Api.Interfaces;
using CleanHome.Review.Api.Repositories;
using CleanHome.Review.Api.Services;

namespace CleanHome.Review.Api.Configuration;

public static class ReviewConfiguration
{
    public static IServiceCollection AddReviewModule(this IServiceCollection services)
    {
        services.AddScoped<IReviewRepository, ReviewRepository>(); services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReviewEligibilityClient, ReviewEligibilityClient>();
        services.AddHttpClient("review-order", client => client.Timeout = TimeSpan.FromSeconds(5)); return services;
    }
}
