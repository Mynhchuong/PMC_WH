namespace Warehouse3D.Models;

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
    public string? ManagerName { get; set; }
    public string? PurposeVi { get; set; }
    public string? PurposeEn { get; set; }
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
    public int MaterialId { get; set; }
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

public class WarehouseMapViewModel
{
    public required WarehouseDefinition Warehouse { get; set; }
    public List<WarehouseTierDto> Tiers { get; set; } = new();
    public int TotalTiers { get; set; }
    public int OccupiedTiers { get; set; }
    public int TotalQr { get; set; }
}

/// <summary>1 ô (vị trí) của 1 tầng, thuộc schema "generic" (bảng WH_RackLocations) — 1 tầng có
/// thể có nhiều ô, khác PMC (1 tầng = 1 vị trí duy nhất). CellNo/LevelNo đều đếm từ 1.</summary>
public class GenericLocationDto
{
    public int LevelNo { get; set; }
    public int CellNo { get; set; }
    public string? Code { get; set; }
}

/// <summary>1 kệ thuộc schema "generic" — Side 'L'/'R' quanh lối đi chính, Front/Back mỗi cái là
/// danh sách ô theo từng tầng (rỗng nếu kệ 1 mặt, không áp lưng).</summary>
public class GenericRackDto
{
    public string RackId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Side { get; set; } = "L";
    public string? Label { get; set; }
    public string? Note { get; set; }
    public int GapBefore { get; set; }
    public List<GenericLocationDto> Front { get; set; } = new();
    public List<GenericLocationDto> Back { get; set; } = new();
}

public class GenericWarehouseMapViewModel
{
    public required WarehouseDefinition Warehouse { get; set; }
    public List<GenericRackDto> Racks { get; set; } = new();
    public int TotalRacks { get; set; }
    public int TotalLocations { get; set; }
    public int FilledLocations { get; set; }
}
