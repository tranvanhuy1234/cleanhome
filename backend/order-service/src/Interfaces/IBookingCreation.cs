using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Models;
namespace CleanHome.Order.Api.Interfaces;
public interface IBookingCreationRepository
{
 Task<BookingCreated?> ReplayAsync(long customer,string key,string hash,CancellationToken ct);
 Task<BookingCreated> CreateAsync(BookingCreation booking,CancellationToken ct);
}
public interface IBookingDependencies
{
 Task<BookingService> ServiceAsync(long id,CancellationToken ct);
 Task<BookingAddress> AddressAsync(long id,string token,CancellationToken ct);
 Task<IReadOnlyList<BookingBranch>> BranchesAsync(CancellationToken ct);
}
public interface IBookingCreationService
{
 Task<BookingCreated> CreateAsync(long customer,string token,CreateBookingRequest request,CancellationToken ct);
 Task<IReadOnlyList<BookingBranch>> BranchesAsync(CancellationToken ct);
}
