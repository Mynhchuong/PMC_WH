using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Helpers;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class MaterialsController : Controller
{
    private static readonly string[] DateFormats = { "d/M/yyyy H:mm:ss", "d/M/yyyy", "yyyy-MM-dd" };
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public MaterialsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View(new MaterialImportResultViewModel());
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Chưa chọn file Excel.");
            return View(new MaterialImportResultViewModel());
        }

        List<Dictionary<string, string?>> rawRows;
        await using (var stream = file.OpenReadStream())
        {
            rawRows = ExcelHelper.ReadRows(stream);
        }

        var parsedRows = rawRows
            .Select(MapRow)
            .Where(r => !IsBlankRow(r))
            .ToList();

        var result = new MaterialImportResultViewModel { TotalRowsParsed = parsedRows.Count };

        var client = _httpClientFactory.CreateClient("PmcApi");
        foreach (var batch in parsedRows.Chunk(ExcelHelper.ImportBatchSize))
        {
            var response = await client.PostAsJsonAsync("api/Materials/import-batch", batch);
            response.EnsureSuccessStatusCode();

            var batchResult = await response.Content.ReadFromJsonAsync<MaterialImportBatchApiResult>(ApiJsonOptions);
            if (batchResult == null) continue;

            result.InsertedCount += batchResult.InsertedCount;
            result.Skipped.AddRange(batchResult.Skipped.Select(s => new MaterialImportSkipItem(s.Barcode, s.Reason)));
        }

        return View(result);
    }

    private static MaterialImportRow MapRow(Dictionary<string, string?> row)
    {
        string? Get(string key) => row.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        return new MaterialImportRow
        {
            Dev = Get("DEV"),
            PoNo = Get("PO"),
            Supplier = Get("SUPPLIER"),
            Model = Get("MODEL"),
            Season = Get("SEASON"),
            Stage = Get("STAGE"),
            Colorway = Get("COLORWAY"),
            Component = Get("COMPONENT"),
            // "MAT" column không có cột tương ứng trong PMC_Materials — bỏ qua.
            MatlDescription = Get("MAT'L DESCRIPTION"),
            ColorCode = Get("COLOR CODE"),
            ColorName = Get("COLOR NAME"),
            SizeSpec = Get("SIZE"),
            ArrivalQty = ParseDecimal(Get("A.Q'TY")),
            Unit = Get("UNIT"),
            FocFlag = Get("FOC"),
            ArrivalDate = ParseDate(Get("ATA")),
            CsCode = ParseInt(Get("CS_CODE")),
            Remark = Get("REMARK"),
            Barcode = Get("BARCODE"),
            Testing = ParseYesNo(Get("TESTING")),
            TestRequire = Get("TEST REQUIRE"),
            TestQty = Get("TEST Q'TY"),
            Category = Get("CATEGORY"),
            RequestBy = Get("REQUEST BY"),
            RequestOn = ParseDate(Get("REQUEST ON")),
        };
    }

    private static bool IsBlankRow(MaterialImportRow r) =>
        string.IsNullOrWhiteSpace(r.Barcode) && r.ArrivalQty is null && string.IsNullOrWhiteSpace(r.Model) && string.IsNullOrWhiteSpace(r.Dev);

    private static decimal? ParseDecimal(string? s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static DateTime? ParseDate(string? s) =>
        s != null && DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

    private static int? ParseYesNo(string? s) => s?.Trim().ToUpperInvariant() switch
    {
        "YES" => 1,
        "NO" => 0,
        _ => null,
    };

    private class MaterialImportBatchApiResult
    {
        public int InsertedCount { get; set; }
        public List<MaterialImportSkipApiItem> Skipped { get; set; } = new();
    }

    private record MaterialImportSkipApiItem(string Barcode, string Reason);
}
