namespace PmcWh.Web.Models;

public class WarehouseTier
{
    public int LocationId { get; set; }
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public int QrCount { get; set; }
}

public class WarehouseMapViewModel
{
    public List<WarehouseTier> Tiers { get; set; } = new();
    public int TotalTiers { get; set; }
    public int OccupiedTiers { get; set; }
    public int TotalQr { get; set; }
}

public class RecentActivity
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

public class WarehouseSummary
{
    public int TotalInStock { get; set; }
    public int IssuedToday { get; set; }
    public int InboundToday { get; set; }
}
