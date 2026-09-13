using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Helpers;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

/// <summary>
/// PMC tự quản lý số kệ/số tầng (thêm mới, sửa người quản lý + công dụng, ẩn khi không dùng nữa)
/// thay vì cố định trong DB — xem StorageLocationsController (Api) cho phần đọc/ghi thật.
/// Api trả list phẳng không phân trang (app mobile phụ thuộc shape này), nên phân trang ở đây.
/// </summary>
[Authorize(Roles = "Admin")]
public class StorageLocationsController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public StorageLocationsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10, bool showInactive = false)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var all = await client.GetFromJsonAsync<List<StorageLocationDto>>(
            $"api/StorageLocations?includeInactive={(showInactive ? "true" : "false")}", ApiJsonOptions)
            ?? new List<StorageLocationDto>();
        var filtered = FilterLocations(all, q);

        var totalCount = filtered.Count;
        var pagedItems = pageSize <= 0
            ? filtered
            : filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var totalPages = pageSize <= 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var model = new StorageLocationListViewModel
        {
            Items = pagedItems,
            ShowInactive = showInactive,
            SearchQuery = q,
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Controller = "StorageLocations",
                Action = "Index",
                RouteValues = new Dictionary<string, string?> { ["showInactive"] = showInactive ? "true" : "false", ["q"] = q },
            },
        };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashWarning"] is string warning) ViewData["FlashWarning"] = warning;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;
        if (TempData["ImportSkippedJson"] is string skippedJson)
        {
            model.ImportSkipped = JsonSerializer.Deserialize<List<StorageLocationImportSkip>>(skippedJson);
            model.ImportSkippedCount = TempData["ImportSkippedCount"] as int? ?? model.ImportSkipped?.Count;
            model.ImportInsertedCount = TempData["ImportInsertedCount"] as int?;
            model.ImportUpdatedCount = TempData["ImportUpdatedCount"] as int?;
            model.ImportTotalCount = TempData["ImportTotalCount"] as int?;
        }

        return View(model);
    }

    /// <summary>Xuất toàn bộ danh sách kệ (kể cả đã ẩn) ra Excel — file này PMC sửa lại rồi import
    /// ngược lên bằng chính form Import bên dưới, không cần file mẫu rỗng riêng.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportExcel()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var items = await client.GetFromJsonAsync<List<StorageLocationDto>>("api/StorageLocations?includeInactive=true", ApiJsonOptions)
            ?? new List<StorageLocationDto>();

        var headers = new List<string> { "RACK NO", "LEVEL NO", "CODE", "MANAGER", "PURPOSE (VI)", "PURPOSE (EN)", "NOTE", "STATUS" };
        var rows = items
            .OrderBy(l => l.RackNo).ThenBy(l => l.LevelNo)
            .Select(l => (IReadOnlyList<object?>)new List<object?>
            {
                l.RackNo, l.LevelNo, l.Code, l.ManagerName, l.PurposeVi, l.PurposeEn, l.Note, l.IsActive ? "ACTIVE" : "INACTIVE",
            });

        var bytes = ExcelHelper.WriteRows(headers, rows);
        var fileName = $"PMC_StorageLocations_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Import Excel (file PMC tải từ ExportExcel về sửa rồi đem lên) — khớp theo (RackNo, LevelNo):
    /// đã có thì cập nhật mô tả, chưa có thì thêm mới. Cột CODE/STATUS trong file chỉ để tham khảo,
    /// không đọc lại. Trùng (RackNo, LevelNo) NGAY TRONG FILE bị chặn ở đây (yêu cầu PMC) — chỉ dòng
    /// xuất hiện đầu tiên được xử lý, các dòng trùng sau đó bị liệt vào danh sách lỗi.
    /// Cùng khuôn mẫu với MaterialsController.Index(IFormFile) — xem ghi chú ở đó.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Index(IFormFile file, bool showInactive = false, string? q = null)
    {
        if (file == null || file.Length == 0)
        {
            TempData["FlashError"] = FlashHelper.Msg("noExcelFileSelected");
            return RedirectToAction(nameof(Index), new { showInactive, q });
        }

        List<Dictionary<string, string?>> rawRows;
        await using (var stream = file.OpenReadStream())
        {
            rawRows = ExcelHelper.ReadRows(stream);
        }

        var parsedRows = rawRows.Select(MapImportRow).Where(r => !IsBlankImportRow(r)).ToList();

        // Chặn trùng (RackNo, LevelNo) ngay trong file trước khi gửi lên Api — PMC yêu cầu kiểm tra
        // rõ. Chỉ dòng đầu tiên của mỗi cặp được import, các dòng trùng sau liệt vào lỗi luôn.
        var seen = new HashSet<(int RackNo, int LevelNo)>();
        var toImport = new List<StorageLocationImportRowModel>();
        var skipped = new List<StorageLocationImportSkip>();

        foreach (var row in parsedRows)
        {
            if (row.RackNo is null || row.LevelNo is null)
            {
                // Vẫn hiện số đã đọc được (nếu có) thay vì "?.?" mù mờ — PMC cần biết rõ thiếu ở dòng
                // nào, VD "996.?" nghĩa là có số kệ nhưng thiếu số tầng.
                var partialCode = $"{row.RackNo?.ToString() ?? "?"}.{row.LevelNo?.ToString() ?? "?"}";
                skipped.Add(new StorageLocationImportSkip(partialCode, "Thiếu số kệ hoặc số tầng."));
                continue;
            }

            var key = (row.RackNo.Value, row.LevelNo.Value);
            var code = $"{row.RackNo}.{row.LevelNo}";
            if (!seen.Add(key))
            {
                skipped.Add(new StorageLocationImportSkip(code, "Trùng kệ trong file — chỉ dòng đầu tiên được import."));
                continue;
            }

            toImport.Add(row);
        }

        var insertedCount = 0;
        var updatedCount = 0;
        var batchErrors = new List<string>();

        var client = _httpClientFactory.CreateClient("PmcApi");
        var batchIndex = 0;
        foreach (var batch in toImport.Chunk(ExcelHelper.ImportBatchSize))
        {
            batchIndex++;
            var response = await client.PostAsJsonAsync("api/StorageLocations/import-batch", batch);

            if (!response.IsSuccessStatusCode)
            {
                var problem = await TryReadApiMessageAsync(response);
                batchErrors.Add($"Batch {batchIndex} ({batch.Length} dòng): {problem ?? "lỗi không xác định"}");
                continue;
            }

            var batchResult = await response.Content.ReadFromJsonAsync<StorageLocationImportBatchApiResult>(ApiJsonOptions);
            if (batchResult == null) continue;

            insertedCount += batchResult.InsertedCount;
            updatedCount += batchResult.UpdatedCount;
            foreach (var s in batchResult.Skipped)
            {
                skipped.Add(new StorageLocationImportSkip(s.Code, s.Reason));
            }
        }

        var parts = new List<(string Key, string?[] Args)>
        {
            ("importedLocationsBase", new[] { insertedCount.ToString(), updatedCount.ToString() }),
        };
        if (skipped.Count > 0) parts.Add(("importedSkippedSuffix", new[] { skipped.Count.ToString() }));
        parts.Add(("periodOnly", Array.Empty<string?>()));

        if (parsedRows.Count > 0)
        {
            TempData["FlashSuccess"] = FlashHelper.Compose(parts.ToArray());
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
            // Chỉ mang chi tiết tối đa 500 dòng lỗi qua popup — số thật vẫn hiển thị đúng qua
            // ImportSkippedCount (cùng nguyên tắc chống cookie/session phình to như Materials import).
            const int maxSkippedDetail = 500;
            TempData["ImportSkippedJson"] = JsonSerializer.Serialize(skipped.Take(maxSkippedDetail));
            TempData["ImportSkippedCount"] = skipped.Count;
            TempData["ImportInsertedCount"] = insertedCount;
            TempData["ImportUpdatedCount"] = updatedCount;
            TempData["ImportTotalCount"] = parsedRows.Count;
        }

        return RedirectToAction(nameof(Index), new { showInactive, q });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStorageLocationViewModel newLocation, bool showInactive = false, string? q = null)
    {
        if (!ModelState.IsValid)
        {
            return await RenderIndexWithErrors(newLocation, showInactive, q);
        }

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync("api/StorageLocations", new
        {
            RackNo = newLocation.RackNo!.Value,
            LevelNo = newLocation.LevelNo!.Value,
            newLocation.ManagerName,
            newLocation.PurposeVi,
            newLocation.PurposeEn,
            newLocation.Note,
        });

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, await TryReadApiMessageAsync(response) ?? "Không thể tạo ô kệ.");
            return await RenderIndexWithErrors(newLocation, showInactive, q);
        }

        TempData["FlashSuccess"] = FlashHelper.Msg("addedLocationSuccess", $"{newLocation.RackNo}.{newLocation.LevelNo}");
        return RedirectToAction(nameof(Index), new { showInactive, q });
    }

    /// <summary>Dữ liệu 1 ô kệ (JSON) để đổ vào modal Sửa — Views/StorageLocations/Index.cshtml gọi qua fetch.</summary>
    [HttpGet("StorageLocations/{id:int}/EditData")]
    public async Task<IActionResult> EditData(int id)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var all = await client.GetFromJsonAsync<List<StorageLocationDto>>("api/StorageLocations?includeInactive=true", ApiJsonOptions)
            ?? new List<StorageLocationDto>();
        var item = all.FirstOrDefault(l => l.LocationId == id);
        return item == null ? NotFound() : Json(item, ApiJsonOptions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int locationId, string code, string? managerName, string? purposeVi, string? purposeEn, string? note, bool showInactive = false, string? q = null)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PutAsJsonAsync($"api/StorageLocations/{locationId}", new { managerName, purposeVi, purposeEn, note });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("savedLocationSuccess", code);
        }
        else
        {
            TempData["FlashError"] = await TryReadApiMessageAsync(response) ?? FlashHelper.Msg("saveLocationFailFallback", code);
        }

        return RedirectToAction(nameof(Index), new { showInactive, q });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int locationId, string code, bool showInactive = false, string? q = null)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsync($"api/StorageLocations/{locationId}/toggle-active", null);

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("toggledLocationSuccess", code);
        }
        else
        {
            TempData["FlashError"] = await TryReadApiMessageAsync(response) ?? FlashHelper.Msg("toggleLocationFailFallback", code);
        }

        return RedirectToAction(nameof(Index), new { showInactive, q });
    }

    private async Task<IActionResult> RenderIndexWithErrors(CreateStorageLocationViewModel newLocation, bool showInactive, string? q)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var all = await client.GetFromJsonAsync<List<StorageLocationDto>>(
            $"api/StorageLocations?includeInactive={(showInactive ? "true" : "false")}", ApiJsonOptions)
            ?? new List<StorageLocationDto>();
        var filtered = FilterLocations(all, q);

        var model = new StorageLocationListViewModel
        {
            Items = filtered.Take(10).ToList(),
            NewLocation = newLocation,
            ShowInactive = showInactive,
            SearchQuery = q,
            Pagination = new PaginationViewModel
            {
                Page = 1,
                PageSize = 10,
                TotalCount = filtered.Count,
                TotalPages = (int)Math.Ceiling(filtered.Count / 10.0),
                Controller = "StorageLocations",
                Action = "Index",
                RouteValues = new Dictionary<string, string?> { ["showInactive"] = showInactive ? "true" : "false", ["q"] = q },
            },
        };
        return View(nameof(Index), model);
    }

    /// <summary>Lọc theo mã kệ (VD "14.5" hoặc chỉ "14"), người quản lý, hoặc công dụng (VI/EN) —
    /// không phân biệt hoa/thường, khớp 1 phần. Danh sách chỉ ~200-400 dòng nên lọc trong bộ nhớ.</summary>
    private static List<StorageLocationDto> FilterLocations(List<StorageLocationDto> items, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return items;
        }

        var needle = q.Trim();
        return items.Where(l =>
            l.Code.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            (l.ManagerName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (l.PurposeVi?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (l.PurposeEn?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (l.Note?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();
    }

    /// <summary>Đọc {message: "..."} từ response lỗi của Api một cách an toàn — 1 số lỗi (VD NotFound)
    /// trả về body rỗng, ReadFromJsonAsync ném JsonException nếu gọi thẳng trên body đó.</summary>
    private static async Task<string?> TryReadApiMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            return problem?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>1 dòng đọc từ Excel — Rack/Level chưa chắc hợp lệ (int? để phát hiện thiếu/sai kiểu),
    /// còn lại là string thô, khớp header không phân biệt hoa/thường (VD "Manager" hay "MANAGER").</summary>
    private record StorageLocationImportRowModel(int? RackNo, int? LevelNo, string? ManagerName, string? PurposeVi, string? PurposeEn, string? Note);

    private static StorageLocationImportRowModel MapImportRow(Dictionary<string, string?> row)
    {
        var ci = new Dictionary<string, string?>(row, StringComparer.OrdinalIgnoreCase);
        string? Get(string key) => ci.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        int? GetInt(string key) => int.TryParse(Get(key), out var n) ? n : null;

        return new StorageLocationImportRowModel(
            GetInt("RACK NO"), GetInt("LEVEL NO"),
            Get("MANAGER"), Get("PURPOSE (VI)"), Get("PURPOSE (EN)"), Get("NOTE"));
    }

    private static bool IsBlankImportRow(StorageLocationImportRowModel r) =>
        r.RackNo is null && r.LevelNo is null && string.IsNullOrWhiteSpace(r.ManagerName) &&
        string.IsNullOrWhiteSpace(r.PurposeVi) && string.IsNullOrWhiteSpace(r.PurposeEn) && string.IsNullOrWhiteSpace(r.Note);

    private class StorageLocationImportBatchApiResult
    {
        public int InsertedCount { get; set; }
        public int UpdatedCount { get; set; }
        public List<StorageLocationImportSkipApiItem> Skipped { get; set; } = new();
    }

    private record StorageLocationImportSkipApiItem(string Code, string Reason);

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
