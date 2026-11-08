using CleanHome.ServiceDefaults;
using CleanHome.User.Api.DTOs.Requests;
using CleanHome.User.Api.DTOs.Responses;
using CleanHome.User.Api.Interfaces;
using CleanHome.User.Api.Models;

namespace CleanHome.User.Api.Services;

public sealed class ProfileService(IProfileRepository profiles) : IProfileService
{
    public async Task<ProfileResponse> GetAsync(long userId, CancellationToken ct)
    {
        var profile = await profiles.FindAsync(userId, ct) ?? throw new ApiException(404, "Hồ sơ chưa được tạo. Bạn có thể cập nhật hồ sơ hoặc thử lại sau.");
        return Map(profile);
    }

    public async Task<ProfileResponse> GetByAdminAsync(long userId, CancellationToken ct)
    {
        if (userId <= 0) throw new ApiException(400, "Mã người dùng phải là số nguyên dương.");
        var profile = await profiles.FindAsync(userId, ct) ?? throw new ApiException(404, "Không tìm thấy hồ sơ người dùng.");
        return Map(profile);
    }

    public async Task<ProfileResponse> SaveAsync(long userId, ProfileRequest input, CancellationToken ct)
    {
        var profile = ToModel(userId, input);
        await profiles.SaveAsync(profile, false, ct);
        return await GetAsync(userId, ct);
    }

    public async Task<ProfileResponse> UpdateByAdminAsync(long userId, AdminProfileRequest input, CancellationToken ct)
    {
        if (userId <= 0) throw new ApiException(400, "Mã người dùng phải là số nguyên dương.");
        var profile = await profiles.UpdateAsync(ToModel(userId, input), ct)
            ?? throw new ApiException(404, "Không tìm thấy hồ sơ người dùng.");
        return Map(profile);
    }

    public Task ProvisionAsync(ProvisionProfileRequest input, CancellationToken ct) => profiles.SaveAsync(ToModel(input.UserId, input), true, ct);

    public async Task<IReadOnlyList<AddressResponse>> AddressesAsync(long userId, CancellationToken ct) =>
        (await profiles.AddressesAsync(userId, ct)).Select(Map).ToList();

    public async Task<AddressResponse> AddressAsync(long userId, long id, CancellationToken ct) =>
        (await AddressesAsync(userId, ct)).FirstOrDefault(address => address.Id == id)
        ?? throw new ApiException(404, "Không tìm thấy địa chỉ.");

    public async Task<AddressResponse> AddAddressAsync(long userId, AddressRequest input, CancellationToken ct)
    {
        _ = await GetAsync(userId, ct);
        var address = ToAddress(userId, 0, input);
        var id = await profiles.AddAddressAsync(address, ct);
        return Map(address with { Id = id });
    }

    public async Task UpdateAddressAsync(long userId, long id, AddressRequest input, CancellationToken ct)
    {
        if (!await profiles.UpdateAddressAsync(ToAddress(userId, id, input), ct)) throw new ApiException(404, "Không tìm thấy địa chỉ.");
    }

    public async Task DeleteAddressAsync(long userId, long id, CancellationToken ct)
    {
        if (!await profiles.DeleteAddressAsync(userId, id, ct)) throw new ApiException(404, "Không tìm thấy địa chỉ.");
    }

    private static UserProfile ToModel(long userId, ProfileRequest input)
    {
        var name = input.FullName.Trim();
        if (name.Length < 2) throw new ApiException(400, "Họ tên phải có ít nhất 2 ký tự.");
        var avatar = string.IsNullOrWhiteSpace(input.AvatarUrl) ? null : input.AvatarUrl.Trim();
        if (avatar is not null && (!Uri.TryCreate(avatar, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new ApiException(400, "Ảnh đại diện phải là URL HTTP hoặc HTTPS.");
        return new UserProfile(userId, name, input.Phone, avatar, DateTime.UtcNow, DateTime.UtcNow);
    }

    private static Address ToAddress(long userId, long id, AddressRequest input)
    {
        var recipient = input.Recipient.Trim(); var text = input.AddressText.Trim();
        if (recipient.Length < 2 || text.Length < 5) throw new ApiException(400, "Tên người nhận hoặc địa chỉ chưa hợp lệ.");
        return new Address(id, userId, recipient, input.Phone, text, input.Latitude!.Value, input.Longitude!.Value);
    }

    private static ProfileResponse Map(UserProfile profile) => new(profile.UserId, profile.FullName, profile.Phone, profile.AvatarUrl, profile.CreatedAt, profile.UpdatedAt);
    private static AddressResponse Map(Address address) => new(address.Id, address.Recipient, address.Phone, address.AddressText, address.Latitude, address.Longitude);
}
