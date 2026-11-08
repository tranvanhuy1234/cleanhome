using CleanHome.Catalog.Api.Models;

namespace CleanHome.Catalog.Api.Interfaces;

public interface ICatalogRepository
{
    Task<CatalogServiceItem?> FindActiveByIdAsync(long id, CancellationToken ct);
    Task<(IReadOnlyList<CatalogServiceItem> Items, long Total)> ListActiveAsync(int page, int pageSize, string? search, CancellationToken ct);
}
