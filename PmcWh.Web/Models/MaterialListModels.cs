namespace PmcWh.Web.Models;

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
    public string? MatlDescription { get; set; }
    public string? ColorCode { get; set; }
    public decimal? ArrivalQty { get; set; }
    public decimal? Balance { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public DateTime CreatedAt { get; set; }

    // Đủ cột mô tả còn lại — API list (GET api/Materials) đã SELECT sẵn, hiện thêm ở
    // Materials/Index để xem được nhiều cột hơn, cuộn ngang khi cần.
    public int? CsCode { get; set; }
    public string? Season { get; set; }
    public string? Stage { get; set; }
    public string? Component { get; set; }
    public string? ColorName { get; set; }
    public string? FocFlag { get; set; }
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
    public DateTime? StockedInAt { get; set; }
    public DateTime? LastIssuedAt { get; set; }
    public DateTime? DisposedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MaterialListViewModel : SearchFilterFields
{
    public List<MaterialListItem> Items { get; set; } = new();
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? IsOverdue { get; set; }
    public PaginationViewModel Pagination { get; set; } = new();
    public List<MaterialImportSkipItem>? ImportSkipped { get; set; }
    public int? ImportInsertedCount { get; set; }
    public int? ImportTotalCount { get; set; }
}

public record MaterialImportSkipItem(string Barcode, string Reason);

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
    public DateTime? RequestOn { get; set; }
    public string? MatlType { get; set; }
    public string? Pic { get; set; }
    public string? Mat { get; set; }
    public DateTime? PoDate { get; set; }
    public DateTime? Etd { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal? PaymentPrice { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
    public DateTime? StockedInAt { get; set; }
    public DateTime? LastIssuedAt { get; set; }
    public DateTime? DisposedAt { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<MovementHistoryItem> Movements { get; set; } = new();
}

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

public class EditMaterialFormModel
{
    public int MaterialId { get; set; }
    public decimal? ArrivalQty { get; set; }
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
    public string? ReturnUrl { get; set; }
}
