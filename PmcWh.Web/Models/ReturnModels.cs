namespace PmcWh.Web.Models;

public class ReturnViewModel
{
    public List<MaterialListItem> ReturnableItems { get; set; } = new();
    public List<StorageLocationDto> Locations { get; set; } = new();
}
