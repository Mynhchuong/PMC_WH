namespace PmcWh.Api.Models;

public class MaterialListItem
{
    public int MaterialId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Dev { get; set; }
    public string? PoNo { get; set; }
    public string? Supplier { get; set; }
    public string? Model { get; set; }
    public string? Colorway { get; set; }
    public string? SizeSpec { get; set; }
    public decimal? ArrivalQty { get; set; }
    public decimal? Balance { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
