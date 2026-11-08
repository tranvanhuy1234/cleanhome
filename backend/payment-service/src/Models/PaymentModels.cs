namespace CleanHome.Payment.Api.Models;
public sealed record OrderFact(long BookingId,long CustomerId,string Code,decimal Amount,string Method,string PaymentStatus,string WorkStatus,DateTime Start,bool CancellationPending);
public sealed record Assignment(long Id,long EmployeeId,long UserId,string Status);
public sealed record PaymentOrder(long BookingId,long CustomerId,string Code,decimal Amount,string Method,string Status,long Revision,long? CollectorEmployeeId,long? CollectorUserId,DateTime? CollectedAt,DateTime? PaidAt);
public sealed record PaymentAttempt(long Id,long BookingId,long CustomerId,decimal Amount,string Reference,string Status,string Url,string CreateDate,DateTime ExpiresAt,string? ProviderRef);
public sealed record ProviderResult(string Reference,decimal Amount,string ResponseCode,string TransactionStatus,string TransactionNo,string PayDate,string Fingerprint);
