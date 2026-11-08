namespace CleanHome.Catalog.Api.Models;

public sealed record ManagedService(long Id, long CategoryId, string CategoryName, string Name,
    string Description, string Unit, decimal BasePrice, int DurationMinutes, bool IsActive, bool CategoryIsActive);
public sealed record ServiceCategory(long Id, string Name, string Description, bool IsActive);
