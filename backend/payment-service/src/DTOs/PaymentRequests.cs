using System.ComponentModel.DataAnnotations;
namespace CleanHome.Payment.Api.DTOs;
public sealed class CheckoutRequest{
 [Range(1,long.MaxValue)]public long OrderId{get;set;}
 [Required,RegularExpression("^[A-Za-z0-9_.:-]{8,100}$")]public string IdempotencyKey{get;set;}="";
}
public sealed class CashRequest{[Range(1,long.MaxValue)]public long OrderId{get;set;}public bool Paid{get;set;}}
public sealed class AdminPaymentRequest{
 [Range(1,long.MaxValue)]public long? EmployeeId{get;set;}
 [Required,StringLength(2000,MinimumLength=5)]public string Evidence{get;set;}="";
 public bool Paid{get;set;}
 public long? CaseId{get;set;}
}
