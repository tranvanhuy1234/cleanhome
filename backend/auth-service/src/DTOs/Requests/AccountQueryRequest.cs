using System.ComponentModel.DataAnnotations;

namespace CleanHome.Auth.Api.DTOs.Requests;

public sealed class AccountQueryRequest
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(254)] public string? Search { get; set; }
}
