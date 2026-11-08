using System.ComponentModel.DataAnnotations;

namespace CleanHome.User.Api.DTOs.Requests;

public class ProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$")] public string Phone { get; set; } = "";
    [MaxLength(2048)] public string? AvatarUrl { get; set; }
}

public sealed class ProvisionProfileRequest : ProfileRequest
{
    [Range(1, long.MaxValue)] public long UserId { get; set; }
}

public sealed class AddressRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Recipient { get; set; } = "";
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$")] public string Phone { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 5)] public string AddressText { get; set; } = "";
    [Required, Range(typeof(decimal), "-90", "90")] public decimal? Latitude { get; set; }
    [Required, Range(typeof(decimal), "-180", "180")] public decimal? Longitude { get; set; }
}
