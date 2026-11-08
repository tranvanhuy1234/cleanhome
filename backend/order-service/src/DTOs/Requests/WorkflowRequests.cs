using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CleanHome.Order.Api.DTOs.Requests;

public sealed class AdminOrderQuery : OrderListQuery
{
    public bool Trash{get;set;}
    [Range(1, long.MaxValue)] public long? CustomerId { get; set; }
}
public class WorkflowRequest
{
    [Required, RegularExpression(@"^[A-Za-z0-9_.:-]{8,100}$")] public string IdempotencyKey { get; set; } = "";
    [Range(1, long.MaxValue)] public long? ExpectedVersion { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AssignmentRequest : WorkflowRequest
{
    [Required, MinLength(1), MaxLength(20)] public long[] EmployeeIds { get; set; } = [];
    [Required, MaxLength(50)] public long[] RequiredSkillIds { get; set; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class JobProgressRequest : WorkflowRequest
{
    [Range(1, long.MaxValue)] public long AssignmentId { get; set; }
    [Required, RegularExpression("^(accepted|in_progress|done)$")] public string NextStatus { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UnassignRequest : WorkflowRequest
{
    [Range(1, long.MaxValue)] public long AssignmentId { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CancellationRequest
{
    [Required, StringLength(2000, MinimumLength = 3)] public string Reason { get; set; } = "";
    [Range(1, long.MaxValue)] public long? ExpectedVersion { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ApproveCancellationRequest : WorkflowRequest
{
    [MaxLength(2000)] public string? Note { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OrderNoteRequest
{
    [MaxLength(2000)] public string? Note { get; set; }
    [Range(1, long.MaxValue)] public long? ExpectedVersion { get; set; }
}
