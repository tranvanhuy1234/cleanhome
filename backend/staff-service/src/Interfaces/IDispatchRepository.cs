using CleanHome.ServiceDefaults.DTOs;
using CleanHome.Staff.Api.DTOs.Requests;
using CleanHome.Staff.Api.DTOs.Responses;
namespace CleanHome.Staff.Api.Interfaces;
public interface IDispatchRepository
{
 Task<PresenceResponse> PresenceAsync(long userId,CancellationToken ct);
 Task<PresenceResponse> SavePresenceAsync(long userId,PresenceRequest request,CancellationToken ct);
 Task RegisterDispatchAsync(DispatchRegistration request,CancellationToken ct);
 Task TickDispatchAsync(CancellationToken ct);
 Task<IReadOnlyList<DispatchOffer>> OffersAsync(long userId,CancellationToken ct);
 Task<DispatchOffer> OfferAsync(long userId,long id,CancellationToken ct);
 Task SkipAsync(long userId,long id,CancellationToken ct);
 Task<DispatchStatus?> DispatchStatusAsync(long bookingId,CancellationToken ct);
 Task<IReadOnlyList<DispatchStatus>> DispatchListAsync(CancellationToken ct);
 Task<DispatchSettings> SettingsAsync(CancellationToken ct);
 Task SaveSettingsAsync(DispatchSettings settings,CancellationToken ct);
 Task ControlDispatchAsync(long bookingId,string action,CancellationToken ct);
 Task<object> DispatchAuditAsync(long bookingId,CancellationToken ct);
 Task<object> ReadyEmployeesAsync(CancellationToken ct);
 Task SaveServiceSkillsAsync(long serviceId,long[] skills,CancellationToken ct);
 Task<IReadOnlyList<long>> ServiceSkillsAsync(long serviceId,CancellationToken ct);
 Task ArriveAsync(long userId,long assignment,CancellationToken ct);
 Task SupportAsync(long userId,long assignment,string reason,CancellationToken ct);
 Task<object> SupportListAsync(CancellationToken ct);
 Task ResolveSupportAsync(long id,CancellationToken ct);
}
public interface IActiveStaffClient{Task<HashSet<long>> ActiveAsync(long[] ids,CancellationToken ct);}
