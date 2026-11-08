using CleanHome.Catalog.Api.DTOs.Requests;
using CleanHome.Catalog.Api.DTOs.Responses;

namespace CleanHome.Catalog.Api.Interfaces;

public interface ICatalogService
{
    Task<ServiceResponse> GetAsync(long id, CancellationToken ct);
    Task<ServicePageResponse> ListAsync(ServiceQueryRequest query, CancellationToken ct);
}
