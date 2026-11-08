using System.ComponentModel.DataAnnotations;

namespace CleanHome.Catalog.Api.DTOs.Requests;

public sealed class ServiceQueryRequest
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(150)] public string? Search { get; set; }
}
