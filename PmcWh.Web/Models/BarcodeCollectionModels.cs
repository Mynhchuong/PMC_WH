using System.ComponentModel.DataAnnotations;

namespace PmcWh.Web.Models;

public class BarcodeListDto
{
    public int ListId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public int TotalScans { get; set; }
}

public class BarcodeListItemDto
{
    public int ItemId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public int ScanCount { get; set; }
    public DateTime LastScannedAt { get; set; }
}

public class BarcodeCollectionIndexViewModel
{
    public List<BarcodeListDto> Lists { get; set; } = new();
    public CreateBarcodeListViewModel NewList { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
}

public class CreateBarcodeListViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên list")]
    [Display(Name = "Tên list")]
    public string Name { get; set; } = string.Empty;
}

public class BarcodeCollectionDetailViewModel
{
    public int ListId { get; set; }
    public string ListName { get; set; } = string.Empty;
    public List<BarcodeListItemDto> Items { get; set; } = new();
    public int TotalItemCount { get; set; }
    public int TotalScans { get; set; }
    public PaginationViewModel Pagination { get; set; } = new();
}
