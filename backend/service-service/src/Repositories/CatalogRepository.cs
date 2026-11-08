using CleanHome.Catalog.Api.Interfaces;
using CleanHome.Catalog.Api.Models;
using CleanHome.ServiceDefaults;

namespace CleanHome.Catalog.Api.Repositories;

public sealed class CatalogRepository(DatabaseConnectionFactory connections) : ICatalogRepository
{
    public async Task<CatalogServiceItem?> FindActiveByIdAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql("""
            SELECT s.id,s.category_id,c.name,s.name,s.description,s.unit,s.base_price,s.duration_minutes
            FROM services s JOIN service_categories c ON c.id=s.category_id
            WHERE s.id=@id AND s.is_active=1 AND c.is_active=1
            """, ("id", id));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new CatalogServiceItem(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetInt32(7)) : null;
    }

    public async Task<(IReadOnlyList<CatalogServiceItem> Items, long Total)> ListActiveAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        var pattern = search is null ? null : "%" + search.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
        const string source = """
            FROM services s JOIN service_categories c ON c.id=s.category_id
            WHERE s.is_active=1 AND c.is_active=1 AND (@pattern IS NULL OR s.name LIKE @pattern ESCAPE '!')
            """;
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var query = connection.Sql($"""
            SELECT s.id,s.category_id,c.name,s.name,s.description,s.unit,s.base_price,s.duration_minutes
            {source} ORDER BY s.id DESC LIMIT @limit OFFSET @offset
            """, ("pattern", pattern), ("limit", pageSize), ("offset", (page - 1) * pageSize));
        query.Transaction = transaction;
        var items = new List<CatalogServiceItem>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                items.Add(new CatalogServiceItem(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                    reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetInt32(7)));
        await using var count = connection.Sql($"SELECT COUNT(*) {source}", ("pattern", pattern));
        count.Transaction = transaction;
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        await transaction.CommitAsync(ct);
        return (items, total);
    }
}
