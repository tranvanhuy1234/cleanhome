using CleanHome.Catalog.Api.DTOs.Requests;
using CleanHome.Catalog.Api.DTOs.Responses;
using CleanHome.Catalog.Api.Interfaces;
using CleanHome.ServiceDefaults;

namespace CleanHome.Catalog.Api.Services;

public sealed class CatalogService(ICatalogRepository catalog) : ICatalogService
{
    public async Task<ServiceResponse> GetAsync(long id, CancellationToken ct)
    {
        if (id <= 0) throw new ApiException(400, "Mã dịch vụ phải là số nguyên dương.");
        var item = await catalog.FindActiveByIdAsync(id, ct)
            ?? throw new ApiException(404, "Dịch vụ không tồn tại hoặc hiện không khả dụng.");
        return new ServiceResponse(item.Id, item.CategoryId, item.CategoryName, item.Name,
            item.Description, item.Unit, item.BasePrice, item.DurationMinutes);
    }

    public async Task<ServicePageResponse> ListAsync(ServiceQueryRequest query, CancellationToken ct)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 100)
            throw new ApiException(400, "Phân trang không hợp lệ.");
        if (query.Search?.Length > 150) throw new ApiException(400, "Từ khóa tìm kiếm không được vượt quá 150 ký tự.");
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var (items, total) = await catalog.ListActiveAsync(query.Page, query.PageSize, search, ct);
        return new ServicePageResponse(items.Select(item => new ServiceResponse(item.Id, item.CategoryId,
            item.CategoryName, item.Name, item.Description, item.Unit, item.BasePrice, item.DurationMinutes)).ToList(),
            query.Page, query.PageSize, total);
    }
}
