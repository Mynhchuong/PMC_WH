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

    private static readonly List<string> TemplateHeaders = new()
    {
        "DEV", "PO", "SUPPLIER", "MODEL", "SEASON", "STAGE", "COLORWAY", "COMPONENT", "MAT",
        "MAT'L DESCRIPTION", "COLOR CODE", "COLOR NAME", "SIZE", "A.Q'TY", "UNIT", "FOC", "ATA",
        "CS_CODE", "REMARK", "BARCODE", "TESTING", "TEST REQUIRE", "TEST Q'TY", "CATEGORY",
        "REQUEST BY", "REQUEST ON",
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? barcode, string? status, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 20)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var query = $"api/Materials?page={page}&pageSize={pageSize}" +
                    $"&barcode={Uri.EscapeDataString(barcode ?? string.Empty)}" +
                    $"&status={Uri.EscapeDataString(status ?? string.Empty)}" +
                    $"&fromDate={Uri.EscapeDataString(fromDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
                    $"&toDate={Uri.EscapeDataString(toDate?.ToString("yyyy-MM-dd") ?? string.Empty)}";
        var paged = await client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(query, ApiJsonOptions);

        var model = new MaterialListViewModel
        {
            Items = paged?.Items ?? new List<MaterialListItem>(),
            Barcode = barcode,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = paged?.TotalCount ?? 0,
                TotalPages = paged?.TotalPages ?? 0,
                Controller = "Materials",
                Action = "Index",
                RouteValues = new Dictionary<string, string?>
                {
                    ["barcode"] = barcode,
                    ["status"] = status,
                    ["fromDate"] = fromDate?.ToString("yyyy-MM-dd"),
                    ["toDate"] = toDate?.ToString("yyyy-MM-dd"),
                },
            },
        };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashWarning"] is string warning) ViewData["FlashWarning"] = warning;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;
        if (TempData["ImportSkippedJson"] is string skippedJson)
        {
            model.ImportSkipped = JsonSerializer.Deserialize<List<MaterialImportSkipItem>>(skippedJson);
        }

        return View(model);
    }

    [HttpGet("Materials/{id:int}/Detail")]
    public async Task<IActionResult> Detail(int id)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.GetAsync($"api/Materials/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var detail = await response.Content.ReadFromJsonAsync<MaterialDetail>(ApiJsonOptions);
        return PartialView("_MaterialDetail", detail);
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var bytes = ExcelHelper.WriteRows(TemplateHeaders, Enumerable.Empty<IReadOnlyList<object?>>());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PMC_Import_Template.xlsx");
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Index(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["FlashError"] = "Chưa chọn file Excel.";
            return RedirectToAction(nameof(Index));
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

        var insertedCount = 0;
        var skipped = new List<MaterialImportSkipItem>();

        var client = _httpClientFactory.CreateClient("PmcApi");
        foreach (var batch in parsedRows.Chunk(ExcelHelper.ImportBatchSize))
        {
            var response = await client.PostAsJsonAsync("api/Materials/import-batch", batch);
            response.EnsureSuccessStatusCode();

            var batchResult = await response.Content.ReadFromJsonAsync<MaterialImportBatchApiResult>(ApiJsonOptions);
            if (batchResult == null) continue;

            insertedCount += batchResult.InsertedCount;
            skipped.AddRange(batchResult.Skipped.Select(s => new MaterialImportSkipItem(s.Barcode, s.Reason)));
        }

        if (insertedCount > 0)
        {
            TempData["FlashSuccess"] = $"Đã import {insertedCount}/{parsedRows.Count} dòng vào Staging" +
                                        (skipped.Count > 0 ? $", bỏ qua {skipped.Count} dòng." : ".");
        }
        else if (parsedRows.Count > 0)
        {
            TempData["FlashWarning"] = "Không có dòng nào được import — xem chi tiết bên dưới.";
        }
        else
        {
            TempData["FlashWarning"] = "File không có dòng dữ liệu nào để import.";
        }

        if (skipped.Count > 0)
        {
            TempData["ImportSkippedJson"] = JsonSerializer.Serialize(skipped);
        }

        return RedirectToAction(nameof(Index));
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
