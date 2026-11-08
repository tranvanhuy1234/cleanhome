using System.Text.Json;

namespace CleanHome.Order.Api.DTOs.Responses;

public sealed record CancellationResponse(long Id, long BookingId, long RequestedBy, string Reason, string Status,
    DateTime RequestedAt, long? ResolvedBy, DateTime? ResolvedAt, string? AdminNote);
public sealed record OperationResponse(string Id, long BookingId, string Kind, string State, JsonElement? Result,
    int? ErrorStatus, string? ErrorMessage, int Attempts, DateTime CreatedAt, DateTime UpdatedAt);
