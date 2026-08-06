namespace PmcWh.Api.Models;

public class MovementHistoryItem
{
    public int MovementId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string? LocationCode { get; set; }
    public string? Username { get; set; }
    public string? RecipientName { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Note { get; set; }
}

public class ArchiveOverdueRequest
{
    public int UserId { get; set; }
}
