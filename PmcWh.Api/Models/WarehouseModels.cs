namespace PmcWh.Api.Models;

/// <summary>1 ô kệ (1 tầng của 1 kệ) + số mã QR đang thực sự nằm trong kho ở ô đó.
/// Dùng dựng bản đồ kho 3D. QrCount chỉ đếm liệu ĐANG trong kho (InStock/PartiallyIssued),
/// không tính hàng đã xuất/hủy/quá hạn — bản đồ chỉ phản ánh hàng vật lý hiện có.</summary>
public class WarehouseTierDto
{
    public int LocationId { get; set; }
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public int QrCount { get; set; }
}

/// <summary>1 mã QR (1 cây liệu) đang nằm ở 1 ô kệ — hiển thị trong panel chi tiết.</summary>
public class LocationMaterialDto
{
    public int MaterialId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Dev { get; set; }
    public string? Model { get; set; }
    public string? SizeSpec { get; set; }
    public decimal? Balance { get; set; }
    public string? Unit { get; set; }
}

/// <summary>Kết quả tìm barcode: ô kệ chứa nó + thứ tự (Ordinal) để nhảy đúng trang.</summary>
public class FindLocationDto
{
    public int LocationId { get; set; }
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Ordinal { get; set; }
}

/// <summary>1 dòng hoạt động gần đây (lên kệ hoặc xuất kho) — dùng cho màn hình giám sát TV.
/// LocationCode chỉ có khi là Inbound, RecipientName chỉ có khi là Issue.</summary>
public class RecentActivityDto
{
    public string Barcode { get; set; } = string.Empty;
    public string? Dev { get; set; }
    public string? Model { get; set; }
    public decimal Qty { get; set; }
    public string? Unit { get; set; }
    public string? LocationCode { get; set; }
    public string? RecipientName { get; set; }
    public DateTime OccurredAt { get; set; }
}

/// <summary>Số liệu tổng quan toàn kho — dùng cho màn hình giám sát TV.</summary>
public class WarehouseSummaryDto
{
    public int TotalInStock { get; set; }
    public int IssuedToday { get; set; }
    public int InboundToday { get; set; }
}

/// <summary>Toàn bộ dữ liệu cho màn hình giám sát TV: tổng quan + 5 hoạt động gần nhất mỗi loại.</summary>
public class WarehouseDashboardDto
{
    public WarehouseSummaryDto Summary { get; set; } = new();
    public List<RecentActivityDto> RecentInbound { get; set; } = new();
    public List<RecentActivityDto> RecentIssue { get; set; } = new();
}
