using System.Text.Json;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Order.Api.Models;

public sealed record WorkflowBooking(long Id, long CustomerId, long BranchId, DateTime ScheduledStart, DateTime ScheduledEnd, string Status, long Version);
public sealed record CancellationRecord(long Id, long BookingId, long RequestedBy, string Reason, string Status,
    DateTime RequestedAt, long? ResolvedBy, DateTime? ResolvedAt, string? AdminNote);
public sealed record WorkflowEnvelope(StaffWorkflowCommand Command, string RequestHash, long? CancellationId, string? Note);
public sealed record WorkflowOperation(string Id, long BookingId, long ActorId, string IdempotencyKey, string Kind,
    WorkflowEnvelope Payload, string State, JsonElement? Result, int? ErrorStatus, string? ErrorMessage, int Attempts, DateTime CreatedAt, DateTime UpdatedAt);
