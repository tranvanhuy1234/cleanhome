namespace CleanHome.Catalog.Api.Models;

public sealed record CatalogServiceItem(long Id, long CategoryId, string CategoryName, string Name,
    string Description, string Unit, decimal BasePrice, int DurationMinutes);
