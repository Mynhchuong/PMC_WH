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
    public DateTime? PoDate { get; set; }
    public DateTime? Etd { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal? PaymentPrice { get; set; }
    public decimal? Amount { get; set; }

    /// <summary>Mã ô kệ (VD "1.2") — chỉ dùng ở tầng Web để tự Inbound sau khi Import, KHÔNG gửi
    /// lên Api (Api.MaterialImportRow không có field này vì Import chỉ tạo Staging).</summary>
    public string? RackNo { get; set; }

    /// <summary>Số lượng đã xuất TRƯỚC KHI dùng app (backfill data cũ, không có lịch sử
    /// StockMovements thật) — chỉ dùng ở tầng Web để tự gọi Issue sau khi Inbound, xem
    /// MaterialsController (Web) Index POST. Chỉ áp dụng được khi dòng ĐÃ có RackNo hợp lệ.</summary>
    public decimal? Out { get; set; }

    /// <summary>Số lượng còn lại PMC tự nhập cho data cũ (đối chiếu OUT + BALANCE phải = Q'TY) —
    /// KHÔNG ghi thẳng vào cột Balance, chỉ dùng để validate rồi Balance thật vẫn do Inbound+Issue
    /// tự tính ra, xem MaterialsController (Web) Index POST.</summary>
    public decimal? Balance { get; set; }
}
