using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Repositories;
using CleanHome.Order.Api.Services;

namespace CleanHome.Order.Api.Configuration;

public static class OrderConfiguration
{
    public static IServiceCollection AddOrderModule(this IServiceCollection services)
    {
        services.AddScoped<OrderNotificationRepository>();
        services.AddScoped<DispatchNotificationRepository>();
        services.AddScoped<OrderArchiveRepository>();
        services.AddScoped<OrderPaymentRepository>();
        services.AddHostedService<PaymentEnrollmentWorker>();
        services.AddScoped<IDispatchBridge,DispatchBridge>();
        services.AddScoped<DispatchQueueRepository>();
        services.AddHostedService<DispatchQueueWorker>();
        services.AddScoped<IStaffStatisticsClient,StaffStatisticsClient>();
        services.AddScoped<IStaffStatisticsOrderRepository,StaffStatisticsOrderRepository>();
        services.AddScoped<IStaffStatisticsService,StaffStatisticsService>();
        services.AddScoped<IStaffDestinationService,StaffDestinationService>();
        services.AddScoped<IBookingCreationRepository, BookingCreationRepository>();
        services.AddScoped<IBookingDependencies, BookingDependencies>();
        services.AddScoped<IBookingCreationService, BookingCreationService>();
        services.AddHttpClient("booking-dependencies", client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<ICustomerOrderRepository, CustomerOrderRepository>();
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();
        services.AddScoped<IOrderWorkflowRepository, OrderWorkflowRepository>();
        services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
        services.AddScoped<IOrderOperationRunner, OrderOperationRunner>();
        services.AddScoped<IStaffWorkflowClient, StaffWorkflowClient>();
        services.AddHttpClient("staff-workflow", client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddHostedService<OrderWorkflowWorker>();
        return services;
    }
}
