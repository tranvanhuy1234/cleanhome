using CleanHome.ServiceDefaults.DTOs;
namespace CleanHome.Order.Api.Interfaces;
public interface IDispatchBridge
{
 Task RegisterAsync(DispatchRegistration registration,CancellationToken ct);
 Task<DispatchStatus?> StatusAsync(long bookingId,CancellationToken ct);
 Task<DispatchOffer> OfferAsync(long userId,long offerId,CancellationToken ct);
}
