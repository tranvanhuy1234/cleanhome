using System.ComponentModel.DataAnnotations;
namespace CleanHome.ServiceDefaults.DTOs;
public sealed record DispatchRegistration(long BookingId,string Code,long BranchId,string Area,long ServiceId,string ServiceName,DateTime Start,DateTime End,decimal Amount,int RequiredStaff,decimal Latitude,decimal Longitude,DateTime? CreatedAt=null);
public sealed record DispatchStatus(long BookingId,string State,int RequiredStaff,int Filled,int Accepted,int Round,DateTime? OfferUntil,DateTime SearchUntil);
public sealed record DispatchOffer(long Id,long BookingId,string Code,string ServiceName,string Area,DateTime Start,DateTime End,decimal Amount,decimal EstimatedShare,int RequiredStaff,int Remaining,decimal? DistanceKm,DateTime ExpiresAt,string State);
public sealed class DispatchSettings:IValidatableObject
{
 public bool Enabled{get;set;}=true;
 [Range(10,300)]public int OfferSeconds{get;set;}=60;
 [Range(30,1800)]public int SearchSeconds{get;set;}=180;
 [Range(30,1800)]public int PositionTtlSeconds{get;set;}=300;
 [Range(10,5000)]public int MaxAccuracyMeters{get;set;}=500;
 [Range(0.1,100)]public double FirstRadiusKm{get;set;}=5;
 [Range(0.1,200)]public double SecondRadiusKm{get;set;}=10;
 [Range(1,100)]public int GroupSize{get;set;}=10;
 public bool AllowBranchFallback{get;set;}=true;
 public IEnumerable<ValidationResult> Validate(ValidationContext context){if(SecondRadiusKm<FirstRadiusKm||SearchSeconds<OfferSeconds)yield return new("Bán kính thứ hai phải lớn hơn và hạn tìm người không ngắn hơn hạn lời mời.");}
}
