namespace PmcWh.Api.Models;

public class MaterialDetail
{
    public int MaterialId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public int? CsCode { get; set; }
    public string? Dev { get; set; }
    public string? PoNo { get; set; }
    public string? Supplier { get; set; }
    public string? Model { get; set; }
    public string? Season { get; set; }
    public string? Stage { get; set; }
    public string? Colorway { get; set; }
    public string? Component { get; set; }
    public string? MatlDescription { get; set; }
    public string? ColorCode { get; set; }
    public string? ColorName { get; set; }
    public string? SizeSpec { get; set; }
    public decimal? ArrivalQty { get; set; }
    public string? Unit { get; set; }
    public string? FocFlag { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public string? Remark { get; set; }
    public int? Testing { get; set; }
    public string? TestRequire { get; set; }
    public string? TestQty { get; set; }
    public string? Category { get; set; }
    public string? RequestBy { get; set; }
    public DateTime? RequestOn { get; set; }
    public decimal? Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StockedInAt { get; set; }
    public DateTime? LastIssuedAt { get; set; }
    public DateTime? DisposedAt { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
