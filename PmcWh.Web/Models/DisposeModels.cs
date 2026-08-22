namespace PmcWh.Web.Models;

public class DisposeViewModel : SearchFilterFields
{
    public List<MaterialListItem> DisposableItems { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
}
