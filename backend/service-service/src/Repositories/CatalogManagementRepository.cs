using CleanHome.Catalog.Api.Interfaces;
using CleanHome.Catalog.Api.Models;
using CleanHome.ServiceDefaults;
using MySqlConnector;

namespace CleanHome.Catalog.Api.Repositories;

public sealed class CatalogManagementRepository(DatabaseConnectionFactory connections) : ICatalogManagementRepository
{
    private const string ServiceSelect = """
        SELECT s.id,s.category_id,c.name,s.name,s.description,s.unit,s.base_price,s.duration_minutes,s.is_active,c.is_active
        FROM services s JOIN service_categories c ON c.id=s.category_id
        """;
    private const string CategorySelect = "SELECT id,name,description,is_active FROM service_categories";
    private static ManagedService ReadService(MySqlDataReader r) => new(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3),
        r.GetString(4), r.GetString(5), r.GetDecimal(6), r.GetInt32(7), r.GetBoolean(8), r.GetBoolean(9));
    private static ServiceCategory ReadCategory(MySqlDataReader r) => new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetBoolean(3));
    private static string? Pattern(string? search) => search is null ? null : "%" + search.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";

    public async Task<ICatalogWriteTransaction> BeginAsync(CancellationToken ct)
    {
        var connection = await connections.OpenAsync(ct);
        try { return new WriteTransaction(connection, await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct)); }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<ManagedService?> FindServiceAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql(ServiceSelect + " WHERE s.id=@id", ("id", id));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadService(reader) : null;
    }

    public async Task<ServiceCategory?> FindCategoryAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql(CategorySelect + " WHERE id=@id", ("id", id));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadCategory(reader) : null;
    }

    public async Task<(IReadOnlyList<ManagedService> Items, long Total)> ListServicesAsync(int page, int size, string? search, bool? active, long? categoryId, CancellationToken ct)
    {
        const string filter = "WHERE (@pattern IS NULL OR s.name LIKE @pattern ESCAPE '!') AND (@active IS NULL OR s.is_active=@active) AND (@category IS NULL OR s.category_id=@category)";
        var pattern = Pattern(search);
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var command = connection.Sql(ServiceSelect + " " + filter + " ORDER BY s.id DESC LIMIT @size OFFSET @offset",
            ("pattern", pattern), ("active", active), ("category", categoryId), ("size", size), ("offset", (page - 1) * size));
        command.Transaction = transaction;
        var items = new List<ManagedService>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(ReadService(reader));
        await using var count = connection.Sql("SELECT COUNT(*) FROM services s " + filter, ("pattern", pattern), ("active", active), ("category", categoryId));
        count.Transaction = transaction;
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        await transaction.CommitAsync(ct);
        return (items, total);
    }

    public async Task<(IReadOnlyList<ServiceCategory> Items, long Total)> ListCategoriesAsync(int page, int size, string? search, bool? active, CancellationToken ct)
    {
        const string filter = "WHERE (@pattern IS NULL OR name LIKE @pattern ESCAPE '!') AND (@active IS NULL OR is_active=@active)";
        var pattern = Pattern(search);
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var query = connection.Sql(CategorySelect + " " + filter + " ORDER BY id DESC LIMIT @size OFFSET @offset",
            ("pattern", pattern), ("active", active), ("size", size), ("offset", (page - 1) * size));
        query.Transaction = transaction;
        var items = new List<ServiceCategory>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(ReadCategory(reader));
        await using var count = connection.Sql("SELECT COUNT(*) FROM service_categories " + filter, ("pattern", pattern), ("active", active));
        count.Transaction = transaction;
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        await transaction.CommitAsync(ct);
        return (items, total);
    }

    private sealed class WriteTransaction(MySqlConnection connection, MySqlTransaction transaction) : ICatalogWriteTransaction
    {
        private MySqlCommand Command(string text, params (string Name, object? Value)[] values)
        {
            var command = connection.Sql(text, values); command.Transaction = transaction; return command;
        }

        public async Task<ManagedService?> FindServiceAsync(long id, CancellationToken ct)
        {
            // Chỉ khóa hàng service trước; tránh khóa category qua JOIN với thứ tự không rõ ràng.
            await using var row = Command("SELECT id FROM services WHERE id=@id FOR UPDATE", ("id", id));
            if (await row.ExecuteScalarAsync(ct) is null) return null;
            await using var command = Command(ServiceSelect + " WHERE s.id=@id", ("id", id));
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadService(reader) : null;
        }

        public async Task<ServiceCategory?> FindCategoryAsync(long id, CancellationToken ct)
        {
            await using var command = Command(CategorySelect + " WHERE id=@id FOR UPDATE", ("id", id));
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadCategory(reader) : null;
        }

        public async Task<long> InsertServiceAsync(ManagedService service, CancellationToken ct)
        {
            await using var command = ServiceCommand("INSERT INTO services (category_id,name,description,unit,base_price,duration_minutes,is_active) VALUES (@category,@name,@description,@unit,@price,@duration,@active)", service);
            await command.ExecuteNonQueryAsync(ct); return command.LastInsertedId;
        }

        public async Task UpdateServiceAsync(ManagedService service, CancellationToken ct)
        {
            await using var command = ServiceCommand("UPDATE services SET category_id=@category,name=@name,description=@description,unit=@unit,base_price=@price,duration_minutes=@duration,is_active=@active WHERE id=@id", service);
            await command.ExecuteNonQueryAsync(ct);
        }

        private MySqlCommand ServiceCommand(string text, ManagedService service) => Command(text,
            ("id", service.Id), ("category", service.CategoryId), ("name", service.Name), ("description", service.Description),
            ("unit", service.Unit), ("price", service.BasePrice), ("duration", service.DurationMinutes), ("active", service.IsActive));

        public async Task<long> InsertCategoryAsync(ServiceCategory category, CancellationToken ct)
        {
            await using var command = Command("INSERT INTO service_categories (name,description,is_active) VALUES (@name,@description,@active)",
                ("name", category.Name), ("description", category.Description), ("active", category.IsActive));
            await command.ExecuteNonQueryAsync(ct); return command.LastInsertedId;
        }

        public async Task UpdateCategoryAsync(ServiceCategory category, CancellationToken ct)
        {
            await using var command = Command("UPDATE service_categories SET name=@name,description=@description,is_active=@active WHERE id=@id",
                ("id", category.Id), ("name", category.Name), ("description", category.Description), ("active", category.IsActive));
            await command.ExecuteNonQueryAsync(ct);
        }

        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public async ValueTask DisposeAsync() { await transaction.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
