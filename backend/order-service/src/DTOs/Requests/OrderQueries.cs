using System.ComponentModel.DataAnnotations;

namespace CleanHome.Order.Api.DTOs.Requests;

public class OrderHistoryQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(150)] public string? Search { get; set; }
    [Range(1, long.MaxValue)] public long? ServiceId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public class OrderListQuery : OrderHistoryQuery
{
    [RegularExpression("^(pending|confirmed|in_progress|completed|cancelled)$")]
    public string? Status { get; set; }
}
