namespace PmcWh.Api.Models;

public class MaterialImportBatchResult
{
    public int InsertedCount { get; set; }
    public List<MaterialImportSkip> Skipped { get; set; } = new();
}

public record MaterialImportSkip(string Barcode, string Reason);
