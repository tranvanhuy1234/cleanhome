using CleanHome.User.Api.DTOs.Requests;
using CleanHome.User.Api.DTOs.Responses;

namespace CleanHome.User.Api.Interfaces;

public interface IProfileService
{
    Task<ProfileResponse> GetAsync(long userId, CancellationToken ct);
    Task<ProfileResponse> GetByAdminAsync(long userId, CancellationToken ct);
    Task<ProfileResponse> SaveAsync(long userId, ProfileRequest input, CancellationToken ct);
    Task<ProfileResponse> UpdateByAdminAsync(long userId, AdminProfileRequest input, CancellationToken ct);
    Task ProvisionAsync(ProvisionProfileRequest input, CancellationToken ct);
    Task<IReadOnlyList<AddressResponse>> AddressesAsync(long userId, CancellationToken ct);
    Task<AddressResponse> AddressAsync(long userId, long id, CancellationToken ct);
    Task<AddressResponse> AddAddressAsync(long userId, AddressRequest input, CancellationToken ct);
    Task UpdateAddressAsync(long userId, long id, AddressRequest input, CancellationToken ct);
    Task DeleteAddressAsync(long userId, long id, CancellationToken ct);
}
