namespace PmcWh.Web.Models;

public class MaterialImportResultViewModel
{
    public int TotalRowsParsed { get; set; }
    public int InsertedCount { get; set; }
    public List<MaterialImportSkipItem> Skipped { get; set; } = new();
}

public record MaterialImportSkipItem(string Barcode, string Reason);
