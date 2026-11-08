namespace CleanHome.Catalog.Api.DTOs.Responses;

public sealed record AdminServiceResponse(long Id, long CategoryId, string CategoryName, string Name,
    string Description, string Unit, decimal BasePrice, int DurationMinutes, bool IsActive, bool CategoryIsActive);
public sealed record AdminServicePage(IReadOnlyList<AdminServiceResponse> Items, int Page, int PageSize, long Total);
public sealed record CategoryResponse(long Id, string Name, string Description, bool IsActive);
public sealed record CategoryPage(IReadOnlyList<CategoryResponse> Items, int Page, int PageSize, long Total);
