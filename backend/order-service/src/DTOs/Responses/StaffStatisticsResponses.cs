namespace CleanHome.Order.Api.DTOs.Responses;
public sealed record StaffStatisticsSummary(int Received,int Completed,int Incomplete,int Cancelled,int Waiting,int Accepted,int InProgress,int AwaitingCompletion,decimal AllocatedRevenue,int SelfCancelled=0,int AdminCancelled=0,int CustomerCancelled=0,int Replacement=0);
public sealed record StaffRevenuePoint(string Period,decimal Revenue,int Completed);
public sealed record StaffCompletedOrder(long Id,string Code,DateTime CompletedAt,string Services,decimal OrderAmount,int Participants,decimal AllocatedRevenue,string Status);
public sealed record StaffCompletedPage(IReadOnlyList<StaffCompletedOrder> Items,int Page,int PageSize,int Total);
public sealed record StaffStatisticsResponse(DateOnly From,DateOnly To,string GroupBy,StaffStatisticsSummary Summary,IReadOnlyList<StaffRevenuePoint> Revenue,StaffCompletedPage History);
