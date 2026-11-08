using CleanHome.Payment.Api.Models;
namespace CleanHome.Payment.Api.Interfaces;
public interface IPaymentRepository{
 Task<PaymentOrder> Enroll(OrderFact fact,CancellationToken ct);
 Task<PaymentOrder> Read(long id,CancellationToken ct);
 Task<PaymentAttempt> Create(OrderFact fact,string key,string ip,Func<string,decimal,string,DateTime,string,string> url,CancellationToken ct);
 Task<PaymentAttempt?> Attempt(string reference,CancellationToken ct);
 Task<string> Confirm(ProviderResult result,CancellationToken ct);
 Task Mutate(long id,long actor,string action,string note,long? employee,long? user,CancellationToken ct);
 Task<IReadOnlyList<long>> Due(CancellationToken ct);
 Task SyncMark(long id,long revision,CancellationToken ct);
 Task Cancel(long id,CancellationToken ct);
 Task<object> AdminList(int page,string? status,CancellationToken ct);
 Task<object> Audit(long id,CancellationToken ct);
 Task<IReadOnlyList<string>> PendingAttempts(CancellationToken ct);
 Task Refund(long id,long caseId,long actor,string evidence,CancellationToken ct);
 Task<bool> ReserveQuery(string reference,CancellationToken ct);
 Task<bool> ArchiveSafe(long id,bool managed,bool cancelled,CancellationToken ct);
}
