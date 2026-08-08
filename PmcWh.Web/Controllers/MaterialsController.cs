using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
        "REQUEST ON", "MAT'L TYPE", "PIC", "RACK NO.",
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? barcode, string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue, int page = 1, int pageSize = 20)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var query = $"api/Materials?page={page}&pageSize={pageSize}" +
                    $"&barcode={Uri.EscapeDataString(barcode ?? string.Empty)}" +
                    $"&status={Uri.EscapeDataString(status ?? string.Empty)}" +
                    $"&fromDate={Uri.EscapeDataString(fromDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
                    $"&toDate={Uri.EscapeDataString(toDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
                    $"&isOverdue={Uri.EscapeDataString(isOverdue?.ToString() ?? string.Empty)}";
        var paged = await client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(query, ApiJsonOptions);

        var model = new MaterialListViewModel
        {
            Items = paged?.Items ?? new List<MaterialListItem>(),
            Barcode = barcode,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
            IsOverdue = isOverdue,
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
                    ["isOverdue"] = isOverdue?.ToString(),
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

    [HttpGet("Materials/{id:int}/EditData")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditData(int id)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.GetAsync($"api/Materials/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var detail = await response.Content.ReadFromJsonAsync<MaterialDetail>(ApiJsonOptions);
        return Json(detail, ApiJsonOptions);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditMaterialFormModel form)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var client = _httpClientFactory.CreateClient("PmcApi");

        var response = await client.PostAsJsonAsync($"api/Materials/{form.MaterialId}/edit", new
        {
            form.ArrivalQty,
            form.Dev, form.PoNo, form.Supplier, form.Model, form.Season, form.Stage,
            form.Colorway, form.Component, form.MatlDescription, form.ColorCode, form.ColorName,
            form.SizeSpec, form.Unit, form.FocFlag, form.ArrivalDate, form.Remark, form.Testing,
            form.TestRequire, form.TestQty, form.Category, form.RequestOn,
            form.MatlType, form.Pic, form.Mat,
            UserId = userId,
        });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("savedChangesSuccess");
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? FlashHelper.Msg("saveChangesFailFallback");
        }

        return RedirectToLocal(form.ReturnUrl);
    }

    /// <summary>Redirect an toàn tới URL do client gửi lên (returnUrl) — chỉ chấp nhận local path,
    /// tránh open-redirect nếu returnUrl bị chỉnh thành 1 domain khác.</summary>
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var bytes = ExcelHelper.WriteRows(TemplateHeaders, Enumerable.Empty<IReadOnlyList<object?>>());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PMC_Import_Template.xlsx");
    }

    /// <summary>Xuất Excel danh sách liệu theo đúng bộ lọc đang xem — đủ cột như file PMC yêu cầu,
    /// kèm 3 cột trạng thái hiện tại (STATUS/BALANCE/RACK NO.) ở cuối.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? barcode, string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var query = "api/Materials/export" +
                    $"?barcode={Uri.EscapeDataString(barcode ?? string.Empty)}" +
                    $"&status={Uri.EscapeDataString(status ?? string.Empty)}" +
                    $"&fromDate={Uri.EscapeDataString(fromDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
                    $"&toDate={Uri.EscapeDataString(toDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
                    $"&isOverdue={Uri.EscapeDataString(isOverdue?.ToString() ?? string.Empty)}";

        var items = await client.GetFromJsonAsync<List<MaterialDetail>>(query, ApiJsonOptions) ?? new List<MaterialDetail>();

        // Đủ cột như file PMC yêu cầu (giống hệt file mẫu import, TRỪ "RACK NO." vì đó là cột chỉ
        // dẫn lúc nhập, không có ý nghĩa khi xuất) + toàn bộ thông tin vận hành/lịch sử còn lại —
        // PMC yêu cầu xuất FULL, không được thiếu trường nào có trong hệ thống.
        var headers = new List<string>
        {
            "MATERIAL ID", "DEV", "PO", "SUPPLIER", "MODEL", "SEASON", "STAGE", "COLORWAY", "COMPONENT", "MAT",
            "MAT'L DESCRIPTION", "COLOR CODE", "COLOR NAME", "SIZE", "A.Q'TY", "UNIT", "FOC", "ATA",
            "CS_CODE", "REMARK", "BARCODE", "TESTING", "TEST REQUIRE", "TEST Q'TY", "CATEGORY",
            "REQUEST ON", "MAT'L TYPE", "PIC",
            "STATUS", "BALANCE", "RACK NO. (hiện tại)",
            "NGÀY LÊN KỆ", "XUẤT GẦN NHẤT", "NGÀY HỦY", "QUÁ 90 NGÀY", "NGÀY TẠO", "CẬP NHẬT GẦN NHẤT",
        };
        var rows = items.Select(m => (IReadOnlyList<object?>)new List<object?>
        {
            m.MaterialId, m.Dev, m.PoNo, m.Supplier, m.Model, m.Season, m.Stage, m.Colorway, m.Component, m.Mat,
            m.MatlDescription, m.ColorCode, m.ColorName, m.SizeSpec, m.ArrivalQty, m.Unit, m.FocFlag,
            m.ArrivalDate?.ToString("yyyy-MM-dd"), m.CsCode, m.Remark, m.Barcode,
            m.Testing == 1 ? "YES" : m.Testing == 0 ? "NO" : null, m.TestRequire, m.TestQty,
            m.Category, m.RequestOn?.ToString("yyyy-MM-dd"), m.MatlType, m.Pic,
            m.Status, m.Balance, m.LocationCode,
            m.StockedInAt?.ToString("yyyy-MM-dd HH:mm"), m.LastIssuedAt?.ToString("yyyy-MM-dd HH:mm"),
            m.DisposedAt?.ToString("yyyy-MM-dd HH:mm"), m.IsOverdue ? "YES" : "NO",
            m.CreatedAt.ToString("yyyy-MM-dd HH:mm"), m.UpdatedAt?.ToString("yyyy-MM-dd HH:mm"),
        });

        var bytes = ExcelHelper.WriteRows(headers, rows);
        var fileName = $"PMC_Materials_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Index(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["FlashError"] = FlashHelper.Msg("noExcelFileSelected");
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
        var skippedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchErrors = new List<string>();

        var client = _httpClientFactory.CreateClient("PmcApi");
        var batchIndex = 0;
        foreach (var batch in parsedRows.Chunk(ExcelHelper.ImportBatchSize))
        {
            batchIndex++;
            var response = await client.PostAsJsonAsync("api/Materials/import-batch", batch);

            // Batch này lỗi (VD dữ liệu quá dài) -> ghi nhận rồi qua batch tiếp theo, KHÔNG throw
            // làm crash cả request (trước đây EnsureSuccessStatusCode() sẽ văng lỗi 500 thô và bỏ
            // luôn các batch sau, dù các batch trước đó đã import thành công).
            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
                batchErrors.Add($"Batch {batchIndex} ({batch.Length} dòng): {problem?.Message ?? "lỗi không xác định"}");
                continue;
            }

            var batchResult = await response.Content.ReadFromJsonAsync<MaterialImportBatchApiResult>(ApiJsonOptions);
            if (batchResult == null) continue;

            insertedCount += batchResult.InsertedCount;
            foreach (var s in batchResult.Skipped)
            {
                skipped.Add(new MaterialImportSkipItem(s.Barcode, s.Reason));
                skippedBarcodes.Add(s.Barcode);
            }
        }

        // Dòng có cột "RACK NO." khớp đúng 1 ô kệ thật -> coi như đã lên kệ sẵn (theo yêu cầu PMC),
        // tự Inbound luôn sau Import, không cần quét tay lại. Mã kệ không khớp/để trống -> giữ
        // nguyên Staging (chờ quét sau ở màn Quét lên kệ).
        var inboundedCount = 0;
        var rowsWithRack = parsedRows
            .Where(r => !string.IsNullOrWhiteSpace(r.RackNo) && !string.IsNullOrWhiteSpace(r.Barcode) && !skippedBarcodes.Contains(r.Barcode!))
            .ToList();
        if (rowsWithRack.Count > 0)
        {
            var locations = await client.GetFromJsonAsync<List<StorageLocationDto>>("api/StorageLocations", ApiJsonOptions) ?? new();
            var codeToId = locations.ToDictionary(l => l.Code, l => l.LocationId, StringComparer.OrdinalIgnoreCase);
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            foreach (var row in rowsWithRack)
            {
                if (!codeToId.TryGetValue(row.RackNo!.Trim(), out var locationId)) continue;

                var byBarcodeResp = await client.GetAsync($"api/Materials/by-barcode/{Uri.EscapeDataString(row.Barcode!)}");
                if (!byBarcodeResp.IsSuccessStatusCode) continue;
                var mat = await byBarcodeResp.Content.ReadFromJsonAsync<MaterialListItem>(ApiJsonOptions);
                if (mat == null) continue;

                var inboundResp = await client.PostAsJsonAsync($"api/Materials/{mat.MaterialId}/inbound", new { locationId, userId });
                if (inboundResp.IsSuccessStatusCode) inboundedCount++;
            }
        }

        if (insertedCount > 0)
        {
            var parts = new List<(string Key, string?[] Args)>
            {
                ("importedRowsBase", new[] { insertedCount.ToString(), parsedRows.Count.ToString() }),
            };
            if (inboundedCount > 0)
            {
                parts.Add(("importedAutoShelvedSuffix", new[] { inboundedCount.ToString() }));
            }
            if (skipped.Count > 0)
            {
                parts.Add(("importedSkippedSuffix", new[] { skipped.Count.ToString() }));
            }
            else if (inboundedCount == 0)
            {
                parts.Add(("periodOnly", Array.Empty<string?>()));
            }
            TempData["FlashSuccess"] = FlashHelper.Compose(parts.ToArray());
        }
        else if (parsedRows.Count > 0)
        {
            TempData["FlashWarning"] = FlashHelper.Msg("importNoRowsWarning");
        }
        else
        {
            TempData["FlashWarning"] = FlashHelper.Msg("importEmptyFileWarning");
        }

        if (batchErrors.Count > 0)
        {
            TempData["FlashError"] = $"{batchErrors.Count} batch import lỗi (các batch khác vẫn đã import bình thường): " +
                                      string.Join(" | ", batchErrors);
        }

        if (skipped.Count > 0)
        {
            TempData["ImportSkippedJson"] = JsonSerializer.Serialize(skipped);
        }

        return RedirectToAction(nameof(Index));
    }

    private static MaterialImportRow MapRow(Dictionary<string, string?> row)
    {
        // So khớp không phân biệt hoa/thường — các file thực tế từ IT có khi ghi header dạng
        // "Mat'l Type" thay vì "MAT'L TYPE" như template chuẩn.
        var ci = new Dictionary<string, string?>(row, StringComparer.OrdinalIgnoreCase);
        string? Get(string key) => ci.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

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
            Mat = Get("MAT"),
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
            RequestOn = ParseDate(Get("REQUEST ON")),
            MatlType = Get("MAT'L TYPE"),
            Pic = Get("PIC"),
            RackNo = Get("RACK NO."),
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

    private class ApiMessage
    {
        public string? Message { get; set; }
    }

    private record MaterialImportSkipApiItem(string Barcode, string Reason);
}
