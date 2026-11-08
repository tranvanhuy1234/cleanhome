using System.Text.Json;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;
using MySqlConnector;

namespace CleanHome.Order.Api.Repositories;

public sealed class OrderWorkflowRepository(DatabaseConnectionFactory connections) : IOrderWorkflowRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string OperationColumns = "id,booking_id,actor_id,idempotency_key,kind,payload,state,result,error_status,error_message,attempts,created_at,updated_at";
    private const string CancellationColumns = "id,booking_id,requested_by,reason,status,requested_at,resolved_by,resolved_at,admin_note";
    private static WorkflowOperation ReadOperation(MySqlDataReader r)
    {
        JsonElement? result = null;
        if (!r.IsDBNull(7)) { using var document = JsonDocument.Parse(r.GetString(7)); result = document.RootElement.Clone(); }
        return new(r.GetString(0), r.GetInt64(1), r.GetInt64(2), r.GetString(3), r.GetString(4),
            JsonSerializer.Deserialize<WorkflowEnvelope>(r.GetString(5), Json)!, r.GetString(6), result,
            r.IsDBNull(8) ? null : r.GetInt32(8), r.IsDBNull(9) ? null : r.GetString(9), r.GetInt32(10), r.GetDateTime(11), r.GetDateTime(12));
    }
    private static CancellationRecord ReadCancellation(MySqlDataReader r) => new(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetString(3), r.GetString(4),
        r.GetDateTime(5), r.IsDBNull(6) ? null : r.GetInt64(6), r.IsDBNull(7) ? null : r.GetDateTime(7), r.IsDBNull(8) ? null : r.GetString(8));
    public async Task<IOrderWorkflowTransaction> BeginAsync(CancellationToken ct)
    {
        var c = await connections.OpenAsync(ct);
        try { return new Transaction(c, await c.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct)); }
        catch { await c.DisposeAsync(); throw; }
    }
    public async Task<WorkflowOperation?> OperationAsync(string id, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var q = c.Sql($"SELECT {OperationColumns} FROM workflow_operations WHERE id=@id", ("id", id));
        await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadOperation(r) : null;
    }
    public async Task<IReadOnlyList<string>> DueAsync(CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var q = c.Sql("SELECT id FROM workflow_operations WHERE state='pending' AND next_attempt_at<=UTC_TIMESTAMP(6) ORDER BY created_at LIMIT 20");
        await using var r = await q.ExecuteReaderAsync(ct); var ids = new List<string>(); while (await r.ReadAsync(ct)) ids.Add(r.GetString(0)); return ids;
    }
    public async Task RetryAsync(string id, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var q = c.Sql("UPDATE workflow_operations SET attempts=attempts+1,next_attempt_at=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 5 SECOND),updated_at=UTC_TIMESTAMP(6) WHERE id=@id AND state='pending'", ("id", id));
        await q.ExecuteNonQueryAsync(ct);
    }
    public async Task<IReadOnlyList<CancellationRecord>> CancellationsAsync(long bookingId, CancellationToken ct)
    {
        await using var c = await connections.OpenAsync(ct); await using var q = c.Sql($"SELECT {CancellationColumns} FROM cancellation_requests WHERE booking_id=@id ORDER BY id DESC", ("id", bookingId));
        await using var r = await q.ExecuteReaderAsync(ct); var result = new List<CancellationRecord>(); while (await r.ReadAsync(ct)) result.Add(ReadCancellation(r)); return result;
    }
    private sealed class Transaction(MySqlConnection c, MySqlTransaction t) : IOrderWorkflowTransaction
    {
        public async Task<bool> DispatchReadyAsync(long bookingId,CancellationToken ct)
        {await using var q=Command("SELECT COUNT(*) FROM booking_dispatch WHERE booking_id=@id AND state IN ('queued','awaiting_payment')",("id",bookingId));return Convert.ToInt64(await q.ExecuteScalarAsync(ct))==0;}
        private MySqlCommand Command(string text, params (string, object?)[] values) { var q = c.Sql(text, values); q.Transaction = t; return q; }
        public async Task<WorkflowBooking?> LockBookingAsync(long id, CancellationToken ct)
        {
            await using var q = Command("SELECT id,customer_id,branch_id,scheduled_start,scheduled_end,status,version FROM bookings WHERE id=@id FOR UPDATE", ("id", id));
            await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetDateTime(3), r.GetDateTime(4), r.GetString(5), r.GetInt64(6)) : null;
        }
        public async Task<WorkflowOperation?> OperationAsync(string id, CancellationToken ct)
        {
            await using var q = Command($"SELECT {OperationColumns} FROM workflow_operations WHERE id=@id FOR UPDATE", ("id", id));
            await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadOperation(r) : null;
        }
        public async Task<WorkflowOperation?> FindKeyAsync(long actorId, string key, CancellationToken ct)
        {
            await using var q = Command($"SELECT {OperationColumns} FROM workflow_operations WHERE actor_id=@actor AND idempotency_key=@key", ("actor", actorId), ("key", key));
            await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadOperation(r) : null;
        }
        public async Task<bool> HasOperationAsync(long bookingId, CancellationToken ct)
        {
            await using var q = Command("SELECT COUNT(*) FROM workflow_operations WHERE booking_id=@id AND state='pending'", ("id", bookingId));
            return Convert.ToInt64(await q.ExecuteScalarAsync(ct)) > 0;
        }
        public async Task<CancellationRecord?> PendingCancellationAsync(long bookingId, CancellationToken ct)
        {
            await using var q = Command($"SELECT {CancellationColumns} FROM cancellation_requests WHERE booking_id=@id AND status='pending'", ("id", bookingId));
            await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadCancellation(r) : null;
        }
        public async Task<long> AddCancellationAsync(long bookingId, long actorId, string reason, CancellationToken ct)
        {
            await using var q = Command("INSERT INTO cancellation_requests (booking_id,requested_by,reason) VALUES (@id,@actor,@reason)", ("id", bookingId), ("actor", actorId), ("reason", reason));
            await q.ExecuteNonQueryAsync(ct); return q.LastInsertedId;
        }
        public async Task ResolveCancellationAsync(long id, long actorId, bool approve, string? note, CancellationToken ct)
        {
            await using var q = Command("UPDATE cancellation_requests SET status=@status,resolved_by=@actor,resolved_at=UTC_TIMESTAMP(6),admin_note=@note WHERE id=@id AND status='pending'", ("id", id), ("actor", actorId), ("note", note), ("status", approve ? "approved" : "rejected"));
            if (await q.ExecuteNonQueryAsync(ct) != 1) throw new ApiException(409, "Yêu cầu hủy không còn chờ xử lý.");
        }
        public async Task UpdateBookingAsync(long bookingId, string status, long actorId, string note, CancellationToken ct)
        {
            await using (var q = Command("UPDATE bookings SET status=@status,version=version+1,updated_at=UTC_TIMESTAMP(6) WHERE id=@id", ("status", status), ("id", bookingId))) await q.ExecuteNonQueryAsync(ct);
            await using var history = Command("INSERT INTO booking_status_history (booking_id,changed_by,status,note,created_at) VALUES (@id,@actor,@status,@note,UTC_TIMESTAMP(6))", ("id", bookingId), ("actor", actorId), ("status", status), ("note", note));
            await history.ExecuteNonQueryAsync(ct);
        }
        public async Task ReleaseReservationsAsync(long bookingId, CancellationToken ct)
        {
            await using (var q = Command("UPDATE entitlement_usages u JOIN booking_items i ON i.id=u.booking_item_id SET u.status='released',u.updated_at=UTC_TIMESTAMP(6) WHERE i.booking_id=@id AND u.status='reserved'", ("id", bookingId))) await q.ExecuteNonQueryAsync(ct);
            await using var voucher = Command("UPDATE voucher_redemptions SET status='CANCELLED',cancelled_at=UTC_TIMESTAMP(6) WHERE booking_id=@id AND status='RESERVED'", ("id", bookingId));
            await voucher.ExecuteNonQueryAsync(ct);
            await using var integration = Command("INSERT INTO integration_events (id,event_type,booking_id,payload) VALUES (@event,'booking_cancelled',@id,JSON_OBJECT('bookingId',@id)) ON DUPLICATE KEY UPDATE booking_id=booking_id", ("event", Guid.NewGuid().ToString("N")), ("id", bookingId));
            await integration.ExecuteNonQueryAsync(ct);
        }
        public async Task CompleteReservationsAsync(long bookingId, CancellationToken ct)
        {
            await using (var q = Command("UPDATE entitlement_usages u JOIN booking_items i ON i.id=u.booking_item_id SET u.status='used',u.updated_at=UTC_TIMESTAMP(6) WHERE i.booking_id=@id AND u.status='reserved'", ("id", bookingId))) await q.ExecuteNonQueryAsync(ct);
            await using var voucher = Command("UPDATE voucher_redemptions SET status='USED',used_at=UTC_TIMESTAMP(6) WHERE booking_id=@id AND status='RESERVED'", ("id", bookingId));
            await voucher.ExecuteNonQueryAsync(ct);
        }
        public async Task InsertOperationAsync(WorkflowOperation operation, CancellationToken ct)
        {
            await using var q = Command("INSERT INTO workflow_operations (id,booking_id,actor_id,idempotency_key,kind,payload) VALUES (@id,@booking,@actor,@key,@kind,@payload)",
                ("id", operation.Id), ("booking", operation.BookingId), ("actor", operation.ActorId), ("key", operation.IdempotencyKey), ("kind", operation.Kind), ("payload", JsonSerializer.Serialize(operation.Payload, Json)));
            await q.ExecuteNonQueryAsync(ct);
        }
        public async Task FinishOperationAsync(string id, string state, string? result, int? errorStatus, string? errorMessage, CancellationToken ct)
        {
            await using var q = Command("UPDATE workflow_operations SET state=@state,result=@result,error_status=@error,error_message=@message,updated_at=UTC_TIMESTAMP(6) WHERE id=@id AND state='pending'", ("id", id), ("state", state), ("result", result), ("error", errorStatus), ("message", errorMessage));
            await q.ExecuteNonQueryAsync(ct);
        }
        public Task CommitAsync(CancellationToken ct) => t.CommitAsync(ct);
        public async ValueTask DisposeAsync() { await t.DisposeAsync(); await c.DisposeAsync(); }
    }
}
