using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CleanHome.Auth.Api.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateAccountRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
}
