namespace PmcWh.Api.Models;

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

public class CreateBarcodeListRequest
{
    public string Name { get; set; } = string.Empty;
    public int? UserId { get; set; }
}

public class ScanBarcodeRequest
{
    public string Barcode { get; set; } = string.Empty;
}

public class ScanBarcodeResult
{
    public bool IsDuplicate { get; set; }
    public int ScanCount { get; set; }
}
