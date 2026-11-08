using CleanHome.Catalog.Api.DTOs.Requests;
using CleanHome.Catalog.Api.DTOs.Responses;
using CleanHome.Catalog.Api.Interfaces;
using CleanHome.Catalog.Api.Models;
using CleanHome.ServiceDefaults;

namespace CleanHome.Catalog.Api.Services;

public sealed class CatalogManagementService(ICatalogManagementRepository repository) : ICatalogManagementService
{
    public async Task<AdminServicePage> ListServicesAsync(AdminServiceQuery query, CancellationToken ct)
    {
        ValidatePage(query);
        if (query.CategoryId is <= 0) throw new ApiException(400, "Mã danh mục không hợp lệ.");
        var (items, total) = await repository.ListServicesAsync(query.Page, query.PageSize, Search(query.Search), query.IsActive, query.CategoryId, ct);
        return new AdminServicePage(items.Select(Map).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<AdminServiceResponse> GetServiceAsync(long id, CancellationToken ct)
    {
        ValidateId(id);
        return Map(await repository.FindServiceAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy dịch vụ."));
    }

    public async Task<AdminServiceResponse> SaveServiceAsync(long? id, ServiceWriteRequest request, CancellationToken ct)
    {
        if (id is not null) ValidateId(id.Value);
        var name = Name(request.Name, 150);
        await using var transaction = await repository.BeginAsync(ct);
        if (id is not null && await transaction.FindServiceAsync(id.Value, ct) is null)
            throw new ApiException(404, "Không tìm thấy dịch vụ.");
        var category = await transaction.FindCategoryAsync(request.CategoryId!.Value, ct)
            ?? throw new ApiException(404, "Không tìm thấy danh mục.");
        if (request.IsActive == true && !category.IsActive)
            throw new ApiException(409, "Không thể cung cấp dịch vụ trong danh mục đang ngừng hoạt động.");
        var service = new ManagedService(id ?? 0, category.Id, category.Name, name, request.Description.Trim(), request.Unit.Trim(),
            request.BasePrice!.Value, request.DurationMinutes!.Value, request.IsActive!.Value, category.IsActive);
        if (id is null) service = service with { Id = await transaction.InsertServiceAsync(service, ct) };
        else await transaction.UpdateServiceAsync(service, ct);
        await transaction.CommitAsync(ct);
        return Map(service);
    }

    public async Task<AdminServiceResponse> SetServiceStatusAsync(long id, bool active, CancellationToken ct)
    {
        ValidateId(id);
        await using var transaction = await repository.BeginAsync(ct);
        var service = await transaction.FindServiceAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy dịch vụ.");
        var category = await transaction.FindCategoryAsync(service.CategoryId, ct) ?? throw new ApiException(404, "Không tìm thấy danh mục.");
        if (active && !category.IsActive) throw new ApiException(409, "Danh mục đang ngừng hoạt động; chưa thể cung cấp dịch vụ.");
        service = service with { IsActive = active, CategoryName = category.Name, CategoryIsActive = category.IsActive };
        await transaction.UpdateServiceAsync(service, ct);
        await transaction.CommitAsync(ct);
        return Map(service);
    }

    public async Task<CategoryPage> ListCategoriesAsync(CatalogAdminQuery query, CancellationToken ct)
    {
        ValidatePage(query);
        var (items, total) = await repository.ListCategoriesAsync(query.Page, query.PageSize, Search(query.Search), query.IsActive, ct);
        return new CategoryPage(items.Select(Map).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<CategoryResponse> GetCategoryAsync(long id, CancellationToken ct)
    {
        ValidateId(id);
        return Map(await repository.FindCategoryAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy danh mục."));
    }

    public async Task<CategoryResponse> SaveCategoryAsync(long? id, CategoryWriteRequest request, CancellationToken ct)
    {
        if (id is not null) ValidateId(id.Value);
        var category = new ServiceCategory(id ?? 0, Name(request.Name, 100), request.Description.Trim(), request.IsActive!.Value);
        await using var transaction = await repository.BeginAsync(ct);
        if (id is not null && await transaction.FindCategoryAsync(id.Value, ct) is null)
            throw new ApiException(404, "Không tìm thấy danh mục.");
        if (id is null) category = category with { Id = await transaction.InsertCategoryAsync(category, ct) };
        else await transaction.UpdateCategoryAsync(category, ct);
        await transaction.CommitAsync(ct);
        return Map(category);
    }

    public async Task<CategoryResponse> SetCategoryStatusAsync(long id, bool active, CancellationToken ct)
    {
        ValidateId(id);
        await using var transaction = await repository.BeginAsync(ct);
        var category = await transaction.FindCategoryAsync(id, ct) ?? throw new ApiException(404, "Không tìm thấy danh mục.");
        category = category with { IsActive = active };
        await transaction.UpdateCategoryAsync(category, ct);
        await transaction.CommitAsync(ct);
        return Map(category);
    }

    private static void ValidateId(long id) { if (id <= 0) throw new ApiException(400, "Mã phải là số nguyên dương."); }
    private static void ValidatePage(CatalogAdminQuery query)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 100 || query.Search?.Length > 150)
            throw new ApiException(400, "Tham số tìm kiếm/phân trang không hợp lệ.");
    }
    private static string? Search(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    private static string Name(string name, int maximum)
    {
        var result = name.Trim();
        if (result.Length < 2 || result.Length > maximum) throw new ApiException(400, "Tên chưa hợp lệ.");
        return result;
    }
    private static AdminServiceResponse Map(ManagedService item) => new(item.Id, item.CategoryId, item.CategoryName,
        item.Name, item.Description, item.Unit, item.BasePrice, item.DurationMinutes, item.IsActive, item.CategoryIsActive);
    private static CategoryResponse Map(ServiceCategory item) => new(item.Id, item.Name, item.Description, item.IsActive);
}
