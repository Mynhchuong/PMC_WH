namespace PmcWh.Web.Models;

/// <summary>
/// 3 điều kiện tìm kết hợp (AND) dùng chung cho mọi màn danh sách liệu (CSDL, Quét lên kệ, Xuất
/// hàng, Hủy liệu) — khớp field/q, field2/q2, field3/q3 bên PmcWh.Api MaterialsController
/// (BuildSearchFilter). Các ViewModel liệt kê liệu kế thừa lớp này để dùng chung UI
/// (_SearchConditions partial) và helper build query string (SearchFilterHelper), tránh lặp lại
/// 6 tham số này ở từng trang.
/// </summary>
public class SearchFilterFields
{
    public string? Field { get; set; }
    public string? Q { get; set; }
    public string? Field2 { get; set; }
    public string? Q2 { get; set; }
    public string? Field3 { get; set; }
    public string? Q3 { get; set; }

    public bool HasAnySearch => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(Q2) || !string.IsNullOrWhiteSpace(Q3);
}
