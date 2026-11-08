using CleanHome.Catalog.Api.DTOs.Requests;
using CleanHome.Catalog.Api.DTOs.Responses;

namespace CleanHome.Catalog.Api.Interfaces;

public interface ICatalogManagementService
{
    Task<AdminServicePage> ListServicesAsync(AdminServiceQuery query, CancellationToken ct);
    Task<AdminServiceResponse> GetServiceAsync(long id, CancellationToken ct);
    Task<AdminServiceResponse> SaveServiceAsync(long? id, ServiceWriteRequest request, CancellationToken ct);
    Task<AdminServiceResponse> SetServiceStatusAsync(long id, bool active, CancellationToken ct);
    Task<CategoryPage> ListCategoriesAsync(CatalogAdminQuery query, CancellationToken ct);
    Task<CategoryResponse> GetCategoryAsync(long id, CancellationToken ct);
    Task<CategoryResponse> SaveCategoryAsync(long? id, CategoryWriteRequest request, CancellationToken ct);
    Task<CategoryResponse> SetCategoryStatusAsync(long id, bool active, CancellationToken ct);
}
