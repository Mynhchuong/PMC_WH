namespace PmcWh.Api.Models;

/// <summary>Chỉ gồm các trường mô tả — không cho sửa ArrivalQty/Balance/Status
/// (do nghiệp vụ nhập/xuất/hủy/nhận lại tự tính, sửa tay dễ làm lệch StockMovements).</summary>
public class EditMaterialRequest
{
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
    public string? RequestBy { get; set; }
    public DateTime? RequestOn { get; set; }
    public int UserId { get; set; }
}
