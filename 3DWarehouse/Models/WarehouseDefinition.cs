namespace Warehouse3D.Models;

/// <summary>1 kho trong danh sách kho — khai báo ở <see cref="Warehouse3D.Helpers.WarehouseRegistry"/>,
/// không hard-code tên/route rải rác ở Controller/View.</summary>
public class WarehouseDefinition
{
    /// <summary>Slug dùng trong route (vd "pmc") và trong config section "Warehouses:{Id}:Oracle" —
    /// chỉ chữ thường/số/gạch ngang, không dấu, không khoảng trắng.</summary>
    public required string Id { get; init; }

    /// <summary>Tên hiển thị — card ở trang chủ, banner trên bản đồ 3D (vd "KHO PMC").</summary>
    public required string Name { get; init; }

    /// <summary>Mô tả ngắn hiển thị dưới tên trên card trang chủ.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Material Icons ligature (vd "warehouse", "factory").</summary>
    public string Icon { get; init; } = "warehouse";

    /// <summary>false = kho "sắp có" — vẫn hiện card ở trang chủ nhưng mờ đi, không bấm vào được,
    /// và WarehouseController trả 404 nếu cố truy cập trực tiếp bằng URL.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>"pmc" = schema PMC gốc (PMC_StorageLocations/PMC_Materials/..., 1 tầng = 1 vị trí,
    /// có QR/barcode/dashboard thật). "generic" = bảng dùng chung WH_Racks/WH_RackLocations (1 tầng
    /// có nhiều ô, mỗi ô 1 mã riêng) — dùng cho kho mới chưa có hệ thống QR/barcode riêng, xem
    /// WarehouseController.TryResolve/Map để biết 2 schema này rẽ nhánh SQL + View khác nhau ra sao.</summary>
    public string Schema { get; init; } = "pmc";
}
