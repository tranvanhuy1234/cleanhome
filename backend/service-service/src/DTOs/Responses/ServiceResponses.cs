namespace CleanHome.Catalog.Api.DTOs.Responses;

public sealed record ServiceResponse(long Id, long CategoryId, string CategoryName, string Name,
    string Description, string Unit, decimal BasePrice, int DurationMinutes);
public sealed record ServicePageResponse(IReadOnlyList<ServiceResponse> Items, int Page, int PageSize, long Total);
