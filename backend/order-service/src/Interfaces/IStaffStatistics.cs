using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults.DTOs;
namespace CleanHome.Order.Api.Interfaces;
public interface IStaffStatisticsClient{Task<IReadOnlyList<StaffStatisticsAssignment>> GetAsync(long userId,CancellationToken ct);}
public interface IStaffStatisticsOrderRepository{Task<IReadOnlyList<StaffStatisticsOrder>> GetAsync(IEnumerable<long> ids,CancellationToken ct);}
public interface IStaffStatisticsService{Task<StaffStatisticsResponse> GetAsync(long userId,StaffStatisticsQuery query,CancellationToken ct);}
