using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CleanHome.Catalog.Api.DTOs.Requests;

public class CatalogAdminQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(150)] public string? Search { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class AdminServiceQuery : CatalogAdminQuery
{
    [Range(1, long.MaxValue)] public long? CategoryId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ServiceWriteRequest : IValidatableObject
{
    [Required, Range(1, long.MaxValue)] public long? CategoryId { get; set; }
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(10000)] public string Description { get; set; } = "";
    [Required, StringLength(20)] public string Unit { get; set; } = "";
    [Required, Range(typeof(decimal), "0", "9999999999.99")] public decimal? BasePrice { get; set; }
    [Required, Range(1, int.MaxValue)] public int? DurationMinutes { get; set; }
    [Required] public bool? IsActive { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BasePrice is { } price && decimal.Round(price, 2) != price)
            yield return new ValidationResult("Giá chỉ được có tối đa hai chữ số thập phân.", [nameof(BasePrice)]);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CategoryWriteRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(10000)] public string Description { get; set; } = "";
    [Required] public bool? IsActive { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CatalogStatusRequest
{
    [Required] public bool? IsActive { get; set; }
}
