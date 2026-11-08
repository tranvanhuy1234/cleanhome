using System.ComponentModel.DataAnnotations;
namespace CleanHome.Order.Api.DTOs.Requests;
public sealed class StaffStatisticsQuery
{
 [Required]public DateOnly? From{get;set;}
 [Required]public DateOnly? To{get;set;}
 [RegularExpression("^(day|week|month|year)$")]public string GroupBy{get;set;}="day";
 [Range(1,100000)]public int Page{get;set;}=1;
 [Range(1,100)]public int PageSize{get;set;}=10;
}
