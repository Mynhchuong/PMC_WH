namespace PmcWh.Web.Models;

// StorageLocationDto giờ ở Models/StorageLocationModels.cs (dùng chung cho trang Quản lý kệ).

/// <summary>
/// 1 dòng trong bảng "Cần lên kệ" (Inbound/Index) — gộp chung Mới nhập (Staging) và Nhận lại
/// (Returnable) vào 1 danh sách duy nhất để phân trang chung 1 bảng, giống mọi màn danh sách liệu
/// khác thay vì 2 danh sách rời rạc không phân trang được.
/// </summary>
public class InboundQueueItem
{
    public MaterialListItem Material { get; set; } = null!;

    /// <summary>"New" (mới nhập, chưa từng lên kệ) hoặc "Return" (đang chờ nhận lại).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Số lượng cần lên kệ/nhận lại — New: ArrivalQty; Return: ArrivalQty - Balance (phần còn đang out).</summary>
    public decimal Outstanding { get; set; }
}

public class InboundViewModel : SearchFilterFields
{
    public List<InboundQueueItem> QueueItems { get; set; } = new();
    public int NewCount { get; set; }
    public int ReturnCount { get; set; }
    public List<StorageLocationDto> Locations { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
}
