using CleanHome.Order.Api.DTOs.Responses;
namespace CleanHome.Order.Api.Interfaces;
public interface IStaffDestinationService
{
 Task<StaffDestinationResponse> GetAsync(long bookingId,long staffUserId,CancellationToken ct);
 Task<StaffJobSummaryResponse> SummaryAsync(long bookingId,long staffUserId,CancellationToken ct);
}
