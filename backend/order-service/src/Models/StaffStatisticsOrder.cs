namespace CleanHome.Order.Api.Models;
public sealed record StaffStatisticsOrder(long Id,string Code,string Status,decimal TotalAmount,DateTime? CompletedAt,string Services);
