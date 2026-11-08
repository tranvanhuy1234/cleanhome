using CleanHome.ServiceDefaults.DTOs;
namespace CleanHome.Staff.Api.Interfaces;
public interface IStaffStatisticsRepository
{
 Task<IReadOnlyList<StaffStatisticsAssignment>> GetAsync(long userId,CancellationToken ct);
}
