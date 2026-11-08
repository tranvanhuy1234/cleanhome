using CleanHome.User.Api.Models;

namespace CleanHome.User.Api.Interfaces;

public interface IProfileRepository
{
    Task<UserProfile?> FindAsync(long userId, CancellationToken ct);
    Task<UserProfile?> UpdateAsync(UserProfile profile, CancellationToken ct);
    Task SaveAsync(UserProfile profile, bool createOnly, CancellationToken ct);
    Task<IReadOnlyList<Address>> AddressesAsync(long userId, CancellationToken ct);
    Task<long> AddAddressAsync(Address address, CancellationToken ct);
    Task<bool> UpdateAddressAsync(Address address, CancellationToken ct);
    Task<bool> DeleteAddressAsync(long userId, long addressId, CancellationToken ct);
}
