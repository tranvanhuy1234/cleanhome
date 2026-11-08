using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace CleanHome.Order.Api.DTOs.Requests;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateBookingRequest
{
    [RegularExpression("^(CASH|VNPAY)$")]public string? PaymentMethod{get;set;}
    public bool AutomaticDispatch {get;set;}
    [Range(1,20)]public int RequiredStaff{get;set;}=1;
    public BookingLocationRequest? Location{get;set;}
    [Required, RegularExpression("^[A-Za-z0-9_.:-]{8,100}$")] public string IdempotencyKey { get; set; } = "";
    [Range(1,long.MaxValue)] public long ServiceId { get; set; }
    [Range(1,long.MaxValue)] public long AddressId { get; set; }
    [Range(1,long.MaxValue)] public long BranchId { get; set; }
    [Range(typeof(decimal),"1","100")] public decimal Quantity { get; set; }
    [Required] public DateTimeOffset? ScheduledStart { get; set; }
    [MaxLength(2000)] public string? SpecialRequest { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BookingLocationRequest
{
 [Required,Range(typeof(decimal),"-90","90")]public decimal? Latitude{get;set;}
 [Required,Range(typeof(decimal),"-180","180")]public decimal? Longitude{get;set;}
 [Required,StringLength(2000,MinimumLength=5)]public string AddressText{get;set;}="";
 public bool Confirmed{get;set;}
}
