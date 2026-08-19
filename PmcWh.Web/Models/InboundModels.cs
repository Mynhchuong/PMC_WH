namespace PmcWh.Web.Models;

public class StorageLocationDto
{
    public int LocationId { get; set; }
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string Code { get; set; } = string.Empty;
}

public class InboundViewModel : SearchFilterFields
{
    public List<MaterialListItem> StagingItems { get; set; } = new();
    public List<MaterialListItem> ReturnableItems { get; set; } = new();
    public List<StorageLocationDto> Locations { get; set; } = new();
}
