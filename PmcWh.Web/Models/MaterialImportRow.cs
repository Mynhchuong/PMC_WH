namespace PmcWh.Web.Models;

public class MaterialImportRow
{
    public string? Barcode { get; set; }
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
    public DateTime? RequestOn { get; set; }
    public string? MatlType { get; set; }
    public string? Pic { get; set; }
    public string? Mat { get; set; }

    /// <summary>Mã ô kệ (VD "1.2") — chỉ dùng ở tầng Web để tự Inbound sau khi Import, KHÔNG gửi
    /// lên Api (Api.MaterialImportRow không có field này vì Import chỉ tạo Staging).</summary>
    public string? RackNo { get; set; }
}
