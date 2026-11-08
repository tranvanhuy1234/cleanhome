using System.ComponentModel.DataAnnotations;
namespace CleanHome.Staff.Api.DTOs.Requests;
public sealed class PresenceRequest:IValidatableObject
{
 public bool Ready{get;set;}
 [Range(typeof(decimal),"-90","90")]public decimal? Latitude{get;set;}
 [Range(typeof(decimal),"-180","180")]public decimal? Longitude{get;set;}
 [Range(0,100000)]public decimal? Accuracy{get;set;}
 public DateTimeOffset? ObservedAt{get;set;}
 public IEnumerable<ValidationResult> Validate(ValidationContext context){if(Ready&&(Latitude.HasValue!=Longitude.HasValue||Latitude.HasValue&&(!Accuracy.HasValue||!ObservedAt.HasValue)))yield return new("Cần đủ tọa độ, độ chính xác và thời gian đo GPS.");}
}
