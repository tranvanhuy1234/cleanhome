using CleanHome.Review.Api.Interfaces;
using CleanHome.Review.Api.Models;
using CleanHome.ServiceDefaults;
using MySqlConnector;

namespace CleanHome.Review.Api.Repositories;

public sealed class ReviewRepository(DatabaseConnectionFactory connections) : IReviewRepository
{
    private const string Columns = "id,booking_id,employee_id,customer_id,assignment_id,rating,comment,created_at,is_hidden,hidden_reason,moderated_by,moderated_at";
    private static ReviewRecord Read(MySqlDataReader r) => new(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt32(5), r.GetString(6), r.GetDateTime(7), r.GetBoolean(8),
        r.IsDBNull(9) ? null : r.GetString(9), r.IsDBNull(10) ? null : r.GetInt64(10), r.IsDBNull(11) ? null : r.GetDateTime(11));
    public async Task<long> CreateAsync(ReviewRecord item, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        await using var q = c.Sql("INSERT INTO reviews (booking_id,employee_id,customer_id,assignment_id,rating,comment,created_at) VALUES (@booking,@employee,@customer,@assignment,@rating,@comment,UTC_TIMESTAMP(6))",
            ("booking", item.BookingId), ("employee", item.EmployeeId), ("customer", item.CustomerId), ("assignment", item.AssignmentId), ("rating", item.Rating), ("comment", item.Comment));
        await q.ExecuteNonQueryAsync(ct); return q.LastInsertedId;
    }
    public async Task<ReviewRecord?> FindAsync(long id, long? customerId, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var q = c.Sql($"SELECT {Columns} FROM reviews WHERE id=@id AND (@owner IS NULL OR customer_id=@owner)", ("id", id), ("owner", customerId));
        await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Read(r) : null;
    }
    public async Task<(IReadOnlyList<ReviewRecord> Items, long Total, decimal Average)> ListAsync(long? customerId, bool publicOnly, ReviewFilter f, CancellationToken ct)
    {
        const string where = """
            WHERE (@owner IS NULL OR customer_id=@owner) AND (@public=0 OR is_hidden=0)
            AND (@booking IS NULL OR booking_id=@booking) AND (@employee IS NULL OR employee_id=@employee)
            AND (@rating IS NULL OR rating=@rating) AND (@hidden IS NULL OR is_hidden=@hidden)
            AND (@from IS NULL OR created_at>=@from) AND (@to IS NULL OR created_at<@to)
            """;
        (string, object?)[] values = [("owner", customerId), ("public", publicOnly), ("booking", f.BookingId), ("employee", f.EmployeeId),
            ("rating", f.Rating), ("hidden", f.IsHidden), ("from", f.From), ("to", f.To)];
        await using var c = await connections.OpenAsync(ct); await using var t = await c.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var q = c.Sql($"SELECT {Columns} FROM reviews {where} ORDER BY created_at DESC,id DESC LIMIT @size OFFSET @offset", [.. values, ("size", f.PageSize), ("offset", (f.Page - 1) * f.PageSize)]);
        q.Transaction = t; var rows = new List<ReviewRecord>();
        await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) rows.Add(Read(r));
        long total; decimal average;
        await using (var q2 = c.Sql($"SELECT COUNT(*),COALESCE(AVG(rating),0) FROM reviews {where}", values))
        {
            q2.Transaction = t; await using var r = await q2.ExecuteReaderAsync(ct); await r.ReadAsync(ct);
            total = r.GetInt64(0); average = r.GetDecimal(1);
        }
        await t.CommitAsync(ct); return (rows, total, average);
    }
    public async Task<bool> SetVisibilityAsync(long id, bool hidden, string? reason, long actorId, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct);
        await using var q = c.Sql("UPDATE reviews SET is_hidden=@hidden,hidden_reason=@reason,moderated_by=@actor,moderated_at=UTC_TIMESTAMP(6) WHERE id=@id",
            ("id", id), ("hidden", hidden), ("reason", reason), ("actor", actorId));
        return await q.ExecuteNonQueryAsync(ct) > 0;
    }
}
