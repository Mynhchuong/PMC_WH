namespace PmcWh.Api.Models;

public class StorageLocationDto
{
    public int LocationId { get; set; }
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Người/tổ quản lý ô kệ này (text tự do, VD "Chị Lan", "Tổ vải").</summary>
    public string? ManagerName { get; set; }

    /// <summary>Công dụng — dùng để chứa gì (tiếng Việt).</summary>
    public string? PurposeVi { get; set; }

    /// <summary>Purpose — what it stores (English).</summary>
    public string? PurposeEn { get; set; }

    /// <summary>Ghi chú thêm.</summary>
    public string? Note { get; set; }
}

/// <summary>Body cho POST api/StorageLocations — tạo mới 1 ô kệ (số kệ + số tầng).</summary>
public class CreateStorageLocationRequest
{
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string? ManagerName { get; set; }
    public string? PurposeVi { get; set; }
    public string? PurposeEn { get; set; }
    public string? Note { get; set; }
}

/// <summary>Body cho PUT api/StorageLocations/{id} — sửa thông tin mô tả (không đổi số kệ/tầng).</summary>
public class UpdateStorageLocationRequest
{
    public string? ManagerName { get; set; }
    public string? PurposeVi { get; set; }
    public string? PurposeEn { get; set; }
    public string? Note { get; set; }
}

public class InboundRequest
{
    public int LocationId { get; set; }
    public int UserId { get; set; }
}

/// <summary>1 dòng import kệ từ Excel (POST api/StorageLocations/import-batch) — khớp theo
/// (RackNo, LevelNo): đã có thì cập nhật mô tả, chưa có thì tạo mới. Không đổi IsActive qua đường này.</summary>
public class StorageLocationImportRow
{
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string? ManagerName { get; set; }
    public string? PurposeVi { get; set; }
    public string? PurposeEn { get; set; }
    public string? Note { get; set; }
}

public record StorageLocationImportSkip(string Code, string Reason);

public class StorageLocationImportBatchResult
{
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public List<StorageLocationImportSkip> Skipped { get; set; } = new();
}
