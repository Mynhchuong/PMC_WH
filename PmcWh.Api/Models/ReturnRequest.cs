namespace PmcWh.Api.Models;

public class ReturnRequest
{
    /// <summary>Chỉ bắt buộc khi liệu đang IssuedOut (đã rời kệ, cần chọn kệ mới).
    /// Nếu đang PartiallyIssued (chưa từng rời kệ) thì bỏ qua, tự giữ nguyên CurrentLocationId cũ.</summary>
    public int? LocationId { get; set; }
    public decimal Qty { get; set; }
    public int UserId { get; set; }
}
