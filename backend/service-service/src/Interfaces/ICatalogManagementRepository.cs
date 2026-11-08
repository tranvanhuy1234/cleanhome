using CleanHome.Catalog.Api.Models;

namespace CleanHome.Catalog.Api.Interfaces;

public interface ICatalogManagementRepository
{
    Task<ICatalogWriteTransaction> BeginAsync(CancellationToken ct);
    Task<ManagedService?> FindServiceAsync(long id, CancellationToken ct);
    Task<ServiceCategory?> FindCategoryAsync(long id, CancellationToken ct);
    Task<(IReadOnlyList<ManagedService> Items, long Total)> ListServicesAsync(int page, int size, string? search, bool? active, long? categoryId, CancellationToken ct);
    Task<(IReadOnlyList<ServiceCategory> Items, long Total)> ListCategoriesAsync(int page, int size, string? search, bool? active, CancellationToken ct);
}

public interface ICatalogWriteTransaction : IAsyncDisposable
{
    Task<ManagedService?> FindServiceAsync(long id, CancellationToken ct);
    Task<ServiceCategory?> FindCategoryAsync(long id, CancellationToken ct);
    Task<long> InsertServiceAsync(ManagedService service, CancellationToken ct);
    Task UpdateServiceAsync(ManagedService service, CancellationToken ct);
    Task<long> InsertCategoryAsync(ServiceCategory category, CancellationToken ct);
    Task UpdateCategoryAsync(ServiceCategory category, CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
}
