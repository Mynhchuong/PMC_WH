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

    /// <summary>Số barcode thật đang nằm ở vị trí này (MES.MTL_BAR_BARCODE@inf_m_e, I_AREA =
    /// Code, I_STATUS = 'Y') — 0 nếu Code có nhưng chưa/không còn hàng thật, null nếu chưa join
    /// được (vd lỗi kết nối dblink, không chặn cả trang vì lỗi 1 nguồn phụ).</summary>
    public int? Qty { get; set; }
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

/// <summary>1 dòng thẻ/lô hàng thật (MES.MTL_BAR_BARCODE@inf_m_e) — dùng cho banner tổng quan
/// schema "generic". OutQty = số lượng đã xuất khỏi lô này (chưa có nghiệp vụ ngày xuất cụ thể,
/// sẽ bổ sung khi có câu SQL riêng cho phần OUT).</summary>
public class GenericActivityDto
{
    public string Barcode { get; set; } = string.Empty;
    public string? PoNum { get; set; }
    public string? Style { get; set; }
    public string? Area { get; set; }
    public string? CreateDate { get; set; }
    public decimal? OutQty { get; set; }
}

/// <summary>1 dòng quét XUẤT kho thật (MES.MTL_BAR_SCANNING@inf_m_e, I_STATUS='O') — join sang
/// MTL_BAR_BARCODE (lấy ITEM_ID/VENDOR_ID), MTL_SYSTEM_ITEMS_B (mã hàng/mô tả) và po_vendors (tên
/// NCC). Bảng này chỉ có in/out (quét ra), KHÔNG có thông tin lên kệ (I_AREA ở đây là khu quét, không
/// phải vị trí kệ như GenericActivityDto.Area).</summary>
public class GenericOutboundDto
{
    public string Barcode { get; set; } = string.Empty;
    public string? ItemCode { get; set; }
    public string? Description { get; set; }
    public decimal TxnQty { get; set; }
    public string? Area { get; set; }
    public string? VendorName { get; set; }
    public string? Gather { get; set; }
}

/// <summary>Tổng quan cho banner Plant C — số liệu tự tính từ MES.MTL_BAR_BARCODE@inf_m_e /
/// MES.MTL_BAR_SCANNING@inf_m_e qua dblink, không phải bảng riêng của app nên có thể trả
/// Available=false nếu dblink lỗi.</summary>
public class GenericDashboardDto
{
    public bool Available { get; set; } = true;
    public int TotalInStock { get; set; }
    public int InboundToday { get; set; }
    public int OutboundToday { get; set; }
    public List<GenericActivityDto> RecentInbound { get; set; } = new();
    public List<GenericOutboundDto> RecentOutbound { get; set; } = new();
}

/// <summary>1 dòng tổng hợp tồn theo mã hàng (SEGMENT1) — SUM(Q_QTY) gộp theo vị trí/NCC/trạng thái,
/// CHƯA tách theo lịch cắt (CUT_ORIGINAL) — xem GenericMaterialCutDto cho bảng tách lịch cắt.</summary>
public class GenericMaterialSummaryDto
{
    public string? ItemCode { get; set; }
    public string? Description { get; set; }
    public string? Area { get; set; }
    public string? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? Status { get; set; }
    public decimal TotalQty { get; set; }
}

/// <summary>Giống GenericMaterialSummaryDto nhưng tách thêm theo CUT_ORIGINAL (lịch cắt) — 1 mã hàng
/// có thể nằm ở nhiều đợt cắt khác nhau, mỗi đợt 1 dòng riêng.</summary>
public class GenericMaterialCutDto : GenericMaterialSummaryDto
{
    public string? CutOriginal { get; set; }
}

/// <summary>Kết quả tra cứu liệu theo mã hàng (SEGMENT1) cho banner "Kiếm liệu" — 2 bảng cạnh nhau:
/// Summary (tổng hợp) và Cuts (tách theo lịch cắt), cùng nguồn MES.MTL_BAR_BARCODE@inf_m_e qua dblink
/// nên cũng có thể Available=false nếu dblink lỗi.</summary>
public class GenericMaterialLookupDto
{
    public bool Available { get; set; } = true;
    public List<GenericMaterialSummaryDto> Summary { get; set; } = new();
    public List<GenericMaterialCutDto> Cuts { get; set; } = new();
}

/// <summary>1 barcode thật đang nằm ở 1 vị trí kệ cụ thể (tìm theo mã vị trí trên bản đồ 3D, khớp
/// I_AREA) — anh yêu cầu "tìm ô, show ô đó chứ, all barcode gì thông tin gì như PMC á".</summary>
public class GenericLocationItemDto
{
    public string Barcode { get; set; } = string.Empty;
    public string? PoNum { get; set; }
    public string? Style { get; set; }
    public decimal Qty { get; set; }
}

/// <summary>Kết quả tra theo mã vị trí (LocationCode) cho popup chi tiết khi tìm trên bản đồ 3D.</summary>
public class GenericLocationDetailDto
{
    public bool Available { get; set; } = true;
    public List<GenericLocationItemDto> Items { get; set; } = new();
}
