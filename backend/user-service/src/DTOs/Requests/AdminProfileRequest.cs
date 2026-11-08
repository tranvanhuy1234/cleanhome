using System.Text.Json.Serialization;

namespace CleanHome.User.Api.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AdminProfileRequest : ProfileRequest
{
}
