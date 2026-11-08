using System.ComponentModel.DataAnnotations;

namespace CleanHome.Auth.Api.DTOs.Requests;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$")] public string Phone { get; set; } = "";
}

public sealed class ManagedAccountRequest : RegisterRequest
{
    [Required, RegularExpression("^(customer|staff)$")] public string Role { get; set; } = "staff";
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MaxLength(128)] public string Password { get; set; } = "";
}

public sealed class PasswordRequest
{
    [Required, MaxLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; set; } = "";
}

public sealed class EmailRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MaxLength(128)] public string Password { get; set; } = "";
}

public sealed class AccountStatusRequest
{
    [Required] public bool? Locked { get; set; }
}
