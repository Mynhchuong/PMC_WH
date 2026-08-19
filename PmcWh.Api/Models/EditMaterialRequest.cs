namespace PmcWh.Api.Models;

/// <summary>Các trường mô tả + ArrivalQty (số lượng ban đầu, cho sửa để chỉnh lỗi nhập liệu).
/// Không cho sửa Balance/Status trực tiếp — khi ArrivalQty đổi, Balance tự cộng/trừ đúng phần
/// chênh lệch (giữ nguyên phần đã xuất/nhận lại), xem MaterialsController.Edit.</summary>
public class EditMaterialRequest
{
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
    public int UserId { get; set; }
}
