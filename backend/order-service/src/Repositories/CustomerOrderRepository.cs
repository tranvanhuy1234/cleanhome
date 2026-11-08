using System.Text.Json;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;
using MySqlConnector;

namespace CleanHome.Order.Api.Repositories;

public sealed class CustomerOrderRepository(DatabaseConnectionFactory connections) : ICustomerOrderRepository
{
    private const string SummaryColumns = "b.id,b.code,b.scheduled_start,b.scheduled_end,b.status,b.total_amount,b.created_at";
    private static OrderSummary ReadSummary(MySqlDataReader r) => new(r.GetInt64(0), r.GetString(1), r.GetDateTime(2),
        r.GetDateTime(3), r.GetString(4), r.GetDecimal(5), r.GetDateTime(6));

    public async Task<(IReadOnlyList<OrderSummary> Items, long Total)> ListAsync(long? customerId, OrderFilter filter, CancellationToken ct)
    {
        const string where = """
            WHERE (@admin=FALSE OR (@trash=1 AND EXISTS(SELECT 1 FROM booking_archives a WHERE a.booking_id=b.id AND a.deleted_at IS NOT NULL)) OR (@trash=0 AND NOT EXISTS(SELECT 1 FROM booking_archives a WHERE a.booking_id=b.id AND a.deleted_at IS NOT NULL)))
            AND (@customer IS NULL OR b.customer_id=@customer) AND (@status IS NULL OR b.status=@status)
            AND (@from IS NULL OR b.scheduled_start>=@from) AND (@to IS NULL OR b.scheduled_start<@to)
            AND (@service IS NULL OR EXISTS (SELECT 1 FROM booking_items i WHERE i.booking_id=b.id AND i.service_id=@service))
            AND (@pattern IS NULL OR b.code LIKE @pattern ESCAPE '!' OR EXISTS
              (SELECT 1 FROM booking_items i WHERE i.booking_id=b.id AND i.service_name_snapshot LIKE @pattern ESCAPE '!'))
            """;
        var pattern = filter.Search is null ? null : "%" + filter.Search.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
        (string, object?)[] values = [("admin",filter.AdminOnly),("trash",filter.Trash),("customer", customerId), ("status", filter.Status), ("from", filter.From),
            ("to", filter.To), ("service", filter.ServiceId), ("pattern", pattern)];
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var query = connection.Sql($"SELECT {SummaryColumns} FROM bookings b {where} ORDER BY b.scheduled_start DESC,b.id DESC LIMIT @limit OFFSET @offset",
            [.. values, ("limit", filter.PageSize), ("offset", (filter.Page - 1) * filter.PageSize)]);
        query.Transaction = transaction;
        var items = new List<OrderSummary>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(ReadSummary(reader));
        await using var count = connection.Sql($"SELECT COUNT(*) FROM bookings b {where}", values);
        count.Transaction = transaction;
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        await transaction.CommitAsync(ct);
        return (items, total);
    }

    public async Task<OrderDetail?> FindAsync(long? customerId, long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var query = connection.Sql($"""
            SELECT {SummaryColumns},b.customer_id,b.branch_id,b.address_snapshot,b.special_request,b.subtotal_amount,b.discount_amount,b.version
            FROM bookings b WHERE b.id=@id AND (@customer IS NULL OR b.customer_id=@customer)
            """, ("id", id), ("customer", customerId));
        query.Transaction = transaction;
        OrderSummary summary; long owner, branch, version; JsonElement address; string? special; decimal subtotal, discount;
        await using (var reader = await query.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            summary = ReadSummary(reader); owner = reader.GetInt64(7); branch = reader.GetInt64(8);
            using var json = JsonDocument.Parse(reader.GetString(9)); address = json.RootElement.Clone();
            special = reader.IsDBNull(10) ? null : reader.GetString(10);
            subtotal = reader.GetDecimal(11); discount = reader.GetDecimal(12); version = reader.GetInt64(13);
        }
        var items = new List<OrderItem>();
        await using (var itemQuery = connection.Sql("SELECT id,service_id,service_name_snapshot,quantity,unit_price,unit_snapshot,entitlement_id FROM booking_items WHERE booking_id=@id ORDER BY id", ("id", id)))
        {
            itemQuery.Transaction = transaction;
            await using var reader = await itemQuery.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) items.Add(new OrderItem(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.GetDecimal(3), reader.GetDecimal(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetInt64(6)));
        }
        var history = new List<OrderHistory>();
        await using (var historyQuery = connection.Sql("SELECT id,status,note,changed_by,created_at FROM booking_status_history WHERE booking_id=@id ORDER BY created_at,id", ("id", id)))
        {
            historyQuery.Transaction = transaction;
            await using var reader = await historyQuery.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) history.Add(new OrderHistory(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetDateTime(4)));
        }
        await transaction.CommitAsync(ct);
        return new OrderDetail(summary, owner, branch, address, special, subtotal, discount, version, items, history);
    }
}
