namespace PmcWh.Api.Models;

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

    // Không lưu trực tiếp vào PMC_Materials — dùng để backfill data cũ có sẵn từ trước khi dùng app
    // (đã xuất 1 phần/hết trước đó, không có lịch sử StockMovements thật). Sau khi insert xong,
    // Web MaterialsController gọi lại API Inbound + Issue (y hệt luồng quét tay) để Balance/Status
    // ra đúng và vẫn có StockMovements audit trail đầy đủ — xem MaterialsController (Web) Index POST.
    public decimal? Out { get; set; }
}
