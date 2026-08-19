namespace PmcWh.Web.Models;

public class IssueViewModel : SearchFilterFields
{
    public List<MaterialListItem> IssuableItems { get; set; } = new();
    public List<RecipientDto> Recipients { get; set; } = new();
}
