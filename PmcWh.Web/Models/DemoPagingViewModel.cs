namespace PmcWh.Web.Models;

public class DemoPagingViewModel
{
    public List<DemoItem> Items { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
}

public class DemoItem
{
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public int SoLuong { get; set; }
    public DateTime NgayNhap { get; set; }
}
