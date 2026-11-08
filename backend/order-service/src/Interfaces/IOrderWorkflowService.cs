using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.DTOs.Responses;
using CleanHome.ServiceDefaults.DTOs;

namespace CleanHome.Order.Api.Interfaces;

public interface IOrderWorkflowService
{
    Task<OperationResponse> LifecycleAsync(long id,long actorId,string kind,LifecycleRequest request,long? requestId,CancellationToken ct);
    Task<OperationResponse> ClaimAsync(long id,long offerId,long actorId,WorkflowRequest request,CancellationToken ct);
    Task<OrderPageResponse> AdminListAsync(AdminOrderQuery query, CancellationToken ct);
    Task<OrderDetailResponse> AdminDetailAsync(long id, CancellationToken ct);
    Task<OrderDetailResponse> ConfirmAsync(long id, long actorId, OrderNoteRequest request, CancellationToken ct);
    Task<CancellationResponse> RequestCancellationAsync(long id, long customerId, CancellationRequest request, CancellationToken ct);
    Task<IReadOnlyList<CancellationResponse>> CancellationsAsync(long id, long? owner, CancellationToken ct);
    Task<CancellationResponse> RejectCancellationAsync(long id, long requestId, long actorId, OrderNoteRequest request, CancellationToken ct);
    Task<OperationResponse> AssignAsync(long id, long actorId, AssignmentRequest request, CancellationToken ct);
    Task<OperationResponse> UnassignAsync(long id, long actorId, UnassignRequest request, CancellationToken ct);
    Task<OperationResponse> ApproveCancellationAsync(long id, long requestId, long actorId, ApproveCancellationRequest request, CancellationToken ct);
    Task<OperationResponse> ProgressAsync(long id, long actorId, JobProgressRequest request, CancellationToken ct);
    Task<OperationResponse> OperationAsync(string id, long actorId, bool admin, CancellationToken ct);
    Task<IReadOnlyList<AssignmentSnapshot>> CustomerAssignmentsAsync(long id, long customerId, CancellationToken ct);
}
