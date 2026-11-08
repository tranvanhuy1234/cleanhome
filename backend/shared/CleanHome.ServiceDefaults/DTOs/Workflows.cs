namespace CleanHome.ServiceDefaults.DTOs;

// Hợp đồng transport giữa order và staff; không chia sẻ repository/entity nghiệp vụ.
public sealed record StaffWorkflowCommand(string OperationId, long BookingId, string Kind, long ActorId,
    long BranchId, DateTime ScheduledStart, DateTime ScheduledEnd, long[] EmployeeIds,
    long[] RequiredSkillIds, long? AssignmentId, string? NextStatus,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorkflowContext? Context=null);
public sealed record WorkflowContext(string Reason,long? RequestId=null,string? Source=null);
public sealed record AssignmentSnapshot(long Id, long BookingId, long EmployeeId, long UserId, string Status,
    DateTime ScheduledStart, DateTime ScheduledEnd, DateTime? CheckInAt, DateTime? CheckOutAt, long Version);
public sealed record StaffWorkflowResult(int ActiveCount, int StartedCount, int DoneCount,int RequiredCount=0,string? Outcome=null,long? RequestId=null);
