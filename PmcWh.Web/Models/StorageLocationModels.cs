using System.ComponentModel.DataAnnotations;

namespace PmcWh.Web.Models;

public class StorageLocationDto
{
    public int LocationId { get; set; }
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? ManagerName { get; set; }
    public string? PurposeVi { get; set; }
    public string? PurposeEn { get; set; }
    public string? Note { get; set; }
}

public class StorageLocationListViewModel
{
    public List<StorageLocationDto> Items { get; set; } = new();
    public CreateStorageLocationViewModel NewLocation { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
    public bool ShowInactive { get; set; }
    public string? SearchQuery { get; set; }

    // Kết quả import Excel gần nhất (TempData qua session) — hiện popup giống Materials/Index.
    public List<StorageLocationImportSkip>? ImportSkipped { get; set; }
    public int? ImportSkippedCount { get; set; }
    public int? ImportInsertedCount { get; set; }
    public int? ImportUpdatedCount { get; set; }
    public int? ImportTotalCount { get; set; }
}

public record StorageLocationImportSkip(string Code, string Reason);

public class CreateStorageLocationViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập số kệ")]
    [Range(1, 9999, ErrorMessage = "Số kệ phải lớn hơn 0")]
    [Display(Name = "Số kệ")]
    public int? RackNo { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số tầng")]
    [Range(1, 999, ErrorMessage = "Số tầng phải lớn hơn 0")]
    [Display(Name = "Số tầng")]
    public int? LevelNo { get; set; }

    // Giới hạn khớp đúng độ dài cột Oracle (PMC_StorageLocations.ManagerName/Purpose_Vi/Purpose_En/Note)
    // — báo lỗi ngay ở form thay vì để Api ném ORA-12899.
    [StringLength(120, ErrorMessage = "Người quản lý tối đa 120 ký tự")]
    [Display(Name = "Người quản lý")]
    public string? ManagerName { get; set; }

    [StringLength(400, ErrorMessage = "Công dụng (VI) tối đa 400 ký tự")]
    [Display(Name = "Công dụng (Tiếng Việt)")]
    public string? PurposeVi { get; set; }

    [StringLength(400, ErrorMessage = "Purpose (EN) tối đa 400 ký tự")]
    [Display(Name = "Purpose (English)")]
    public string? PurposeEn { get; set; }

    [StringLength(400, ErrorMessage = "Ghi chú tối đa 400 ký tự")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }
}
