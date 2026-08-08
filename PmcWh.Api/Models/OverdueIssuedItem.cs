namespace PmcWh.Api.Models;

public class OverdueIssuedItem
{
    public int MaterialId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Dev { get; set; }
    public string? PoNo { get; set; }
    public string? MatlDescription { get; set; }
    public string? ColorCode { get; set; }
    public decimal? ArrivalQty { get; set; }
    public decimal? Balance { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
    public string? RecipientName { get; set; }
    public DateTime? LastIssuedAt { get; set; }
    public int DaysOut { get; set; }
}
