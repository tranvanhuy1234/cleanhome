using System.ComponentModel.DataAnnotations;
namespace CleanHome.Staff.Api.DTOs.Requests;
public sealed record WithdrawalPolicy([property:Range(0,10080)]int EarlyMinutes);
