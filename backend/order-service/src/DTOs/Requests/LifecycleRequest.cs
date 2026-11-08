using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace CleanHome.Order.Api.DTOs.Requests;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LifecycleRequest:WorkflowRequest{
 [Required,StringLength(2000,MinimumLength=3)]public string Reason{get;set;}="";
 [Range(1,long.MaxValue)]public long? AssignmentId{get;set;}
 public bool Approve{get;set;}
}
public sealed class ArchiveRequest{[Required,StringLength(2000,MinimumLength=3)]public string Reason{get;set;}="";[Range(1,long.MaxValue)]public long? ExpectedVersion{get;set;}}
