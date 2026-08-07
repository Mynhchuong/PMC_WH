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
