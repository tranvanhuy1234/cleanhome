using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Order.Api.Interfaces;

public interface IStaffWorkflowClient
{
    Task<StaffWorkflowResult> ExecuteAsync(StaffWorkflowCommand command, CancellationToken ct);
    Task<IReadOnlyList<AssignmentSnapshot>> AssignmentsAsync(long bookingId, CancellationToken ct);
}
public interface IOrderOperationRunner
{
    Task ExecuteAsync(string id, CancellationToken ct);
}
