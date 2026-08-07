namespace PmcWh.Api.Models;

public class IssueReportItem
{
    public int MaterialId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Dev { get; set; }
    public string? Model { get; set; }
    public string? Colorway { get; set; }
    public string? SizeSpec { get; set; }
    public string? Unit { get; set; }
    public decimal Qty { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? RecipientName { get; set; }
    public string? Username { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public int? DaysOut { get; set; }
}
