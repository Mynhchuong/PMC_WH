using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PmcWh.Web.Helpers;
using PmcWh.Web.Hubs;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class MaterialsController : Controller
{
    private static readonly string[] DateFormats = { "d/M/yyyy H:mm:ss", "d/M/yyyy", "yyyy-MM-dd" };

    // Recipient đặc biệt cho lượng OUT của data cũ import từ file có sẵn (đã xuất trước khi dùng
    // app, không rõ nơi nhận thật) — tạo sẵn trong PMC_Recipients, xem Index POST bên dưới.
    private const string LegacyDataRecipientName = "Dữ liệu cũ (trước khi dùng app)";
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<WarehouseHub> _hub;

    public MaterialsController(IHttpClientFactory httpClientFactory, IHubContext<WarehouseHub> hub)
    {
        _httpClientFactory = httpClientFactory;
        _hub = hub;
    }

    // Đúng số lượng + thứ tự cột PMC yêu cầu (nhắn 19/8). Cột OUT/BALANCE không lưu trực tiếp vào
    // PMC_Materials — dùng để backfill data cũ có sẵn từ trước khi dùng app (xem xử lý ở Index POST).
    private static readonly List<string> TemplateHeaders = new()
    {
        "DEV", "PODATE", "ETD", "PO", "SUPPLIER", "MODEL", "SEASON", "STAGE", "COLORWAY", "COMPONENT", "MAT",
        "MAT'L DESCRIPTION", "COLOR CODE", "COLOR NAME", "SIZE", "Q'TY", "UNIT",
        "ORIGINAL PRICE", "PAYMENT PRICE", "AMOUNT", "FOC/NON FOC", "ATA (INPUT)", "MAT'L TYPE", "REMARK", "PIC",
        "BARCODE", "TESTING (YES/NO)", "TEST REQUIRE", "TEST Q'TY", "CATEGORY",
        "RACK NO", "OUT", "BALANCE",
    };

    [HttpGet]
    public async Task<IActionResult> Index(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3,
        string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue, int page = 1, int pageSize = 10)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var query = $"api/Materials?page={page}&pageSize={pageSize}" +
                    $"&{SearchFilterHelper.ToQueryString(field, q, field2, q2, field3, q3)}" +
                    $"&status={Uri.EscapeDataString(status ?? string.Empty)}" +
                    $"&fromDate={Uri.EscapeDataString(fromDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
                    $"&toDate={Uri.EscapeDataString(toDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
                    $"&isOverdue={Uri.EscapeDataString(isOverdue?.ToString() ?? string.Empty)}";
        var paged = await client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(query, ApiJsonOptions);

        var model = new MaterialListViewModel
        {
            Items = paged?.Items ?? new List<MaterialListItem>(),
            Field = field,
            Q = q,
            Field2 = field2,
            Q2 = q2,
            Field3 = field3,
            Q3 = q3,
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
                    ["field"] = field,
                    ["q"] = q,
                    ["field2"] = field2,
                    ["q2"] = q2,
                    ["field3"] = field3,
                    ["q3"] = q3,
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
            model.ImportSkippedCount = TempData["ImportSkippedCount"] as int? ?? model.ImportSkipped?.Count;
            model.ImportInsertedCount = TempData["ImportInsertedCount"] as int?;
            model.ImportTotalCount = TempData["ImportTotalCount"] as int?;
        }

        return View(model);
    }

    /// <summary>
    /// Tra materialId theo Barcode — dùng cho các trang chỉ có sẵn chuỗi Barcode chứ không có
    /// MaterialId (VD "Thu thập Barcode", vốn không có FK tới PMC_Materials), để mở được popup
    /// <see cref="Detail"/> vốn cần id. Không phải Barcode nào ở đây cũng khớp 1 liệu thật.
    /// </summary>
    [HttpGet("Materials/ByBarcode/{barcode}")]
    public async Task<IActionResult> ByBarcode(string barcode)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.GetAsync($"api/Materials/by-barcode/{Uri.EscapeDataString(barcode)}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var item = await response.Content.ReadFromJsonAsync<MaterialListItem>(ApiJsonOptions);
        if (item == null)
        {
            return NotFound();
        }

        // Trả nguyên item (không chỉ materialId) — các trang Xuất hàng/Hủy liệu cần Balance/Unit/Status
        // để mở popup thao tác trực tiếp khi quét trúng 1 liệu KHÔNG nằm trên trang hiện tại (đã phân
        // trang, danh sách đầy đủ không còn nằm hết trong DOM như trước).
        return Json(item, ApiJsonOptions);
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
            form.PoDate, form.Etd, form.OriginalPrice, form.PaymentPrice, form.Amount,
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

    /// <summary>Xoá vĩnh viễn nhiều liệu cùng lúc (checkbox chọn nhiều ở Materials/Index, nút "Xóa đã
    /// chọn") — gọi tuần tự từng cái qua API xoá thật DELETE api/Materials/{id} (không có endpoint
    /// batch riêng bên Api), gộp lại 1 thông báo tổng kết thay vì spam nhiều toast, chỉ báo
    /// warehouseChanged 1 lần ở cuối. Đây là cách xoá liệu DUY NHẤT trên trang này.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete(int[] materialIds, string? returnUrl)
    {
        if (materialIds == null || materialIds.Length == 0)
        {
            return RedirectToLocal(returnUrl);
        }

        var client = _httpClientFactory.CreateClient("PmcApi");

        var successCount = 0;
        foreach (var materialId in materialIds)
        {
            var response = await client.DeleteAsync($"api/Materials/{materialId}");
            if (response.IsSuccessStatusCode) successCount++;
        }

        var failCount = materialIds.Length - successCount;
        if (successCount > 0)
        {
            TempData["FlashSuccess"] = failCount > 0
                ? FlashHelper.Msg("bulkMaterialDeletedPartial", successCount.ToString(), materialIds.Length.ToString())
                : FlashHelper.Msg("bulkMaterialDeletedSuccess", successCount.ToString());
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            TempData["FlashError"] = FlashHelper.Msg("bulkMaterialDeletedFail");
        }

        return RedirectToLocal(returnUrl);
    }

    /// <summary>Hủy 1 liệu (Status='Disposed' bên Api, Balance về 0, ghi StockMovements) — dùng cho
    /// nút "Huỷ" nhanh ở Materials/Index. Gọi chung endpoint Api với trang Hủy liệu (DisposeController),
    /// chỉ khác là redirect về lại returnUrl (Materials/Index) thay vì luôn về Dispose/Index.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispose(int materialId, string? barcode, string? returnUrl)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var client = _httpClientFactory.CreateClient("PmcApi");

        var response = await client.PostAsJsonAsync($"api/Materials/{materialId}/dispose", new { UserId = userId });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("disposedSuccess", barcode);
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? FlashHelper.Msg("disposeFailFallback", barcode);
        }

        return RedirectToLocal(returnUrl);
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
    public async Task<IActionResult> ExportExcel(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3,
        string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var query = "api/Materials/export" +
                    $"?{SearchFilterHelper.ToQueryString(field, q, field2, q2, field3, q3)}" +
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
            "MATERIAL ID", "DEV", "PODATE", "ETD", "PO", "SUPPLIER", "MODEL", "SEASON", "STAGE", "COLORWAY", "COMPONENT", "MAT",
            "MAT'L DESCRIPTION", "COLOR CODE", "COLOR NAME", "SIZE", "Q'TY", "UNIT",
            "ORIGINAL PRICE", "PAYMENT PRICE", "AMOUNT", "FOC/NON FOC", "ATA (INPUT)",
            "CS_CODE", "REMARK", "BARCODE", "TESTING (YES/NO)", "TEST REQUIRE", "TEST Q'TY", "CATEGORY",
            "REQUEST ON", "MAT'L TYPE", "PIC",
            "STATUS", "BALANCE", "RACK NO. (hiện tại)",
            "NGÀY LÊN KỆ", "XUẤT GẦN NHẤT", "NGÀY HỦY", "QUÁ 90 NGÀY", "NGÀY TẠO", "CẬP NHẬT GẦN NHẤT",
        };
        var rows = items.Select(m => (IReadOnlyList<object?>)new List<object?>
        {
            m.MaterialId, m.Dev, m.PoDate?.ToString("yyyy-MM-dd"), m.Etd?.ToString("yyyy-MM-dd"), m.PoNo, m.Supplier, m.Model, m.Season, m.Stage, m.Colorway, m.Component, m.Mat,
            m.MatlDescription, m.ColorCode, m.ColorName, m.SizeSpec, m.ArrivalQty, m.Unit,
            m.OriginalPrice, m.PaymentPrice, m.Amount, m.FocFlag,
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
        var inboundedMaterials = new Dictionary<string, (int MaterialId, decimal ArrivalQty)>(StringComparer.OrdinalIgnoreCase);
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
                if (inboundResp.IsSuccessStatusCode)
                {
                    inboundedCount++;
                    inboundedMaterials[row.Barcode!] = (mat.MaterialId, mat.ArrivalQty ?? row.ArrivalQty ?? 0);
                }
            }
        }

        // Dòng có cột "OUT" > 0 (data cũ đã xuất trước khi dùng app, không có lịch sử StockMovements
        // thật) VÀ đã lên kệ thành công ở bước trên -> tự Issue luôn cho recipient đặc biệt
        // LegacyDataRecipientName, để Balance/Status ra đúng mà vẫn giữ StockMovements audit trail
        // (thay vì ghi thẳng Balance vào DB, phá vỡ nguyên tắc "mọi thay đổi tồn kho đều qua nghiệp
        // vụ Inbound/Issue/Return/Dispose"). Nếu PMC có điền thêm BALANCE, đối chiếu OUT+BALANCE
        // phải khớp Q'TY — lệch thì bỏ qua, không tự xuất, tránh xuất sai số lượng.
        var issuedCount = 0;
        var rowsWithOut = parsedRows
            .Where(r => r.Out is > 0 && !string.IsNullOrWhiteSpace(r.Barcode) && inboundedMaterials.ContainsKey(r.Barcode!))
            .ToList();
        if (rowsWithOut.Count > 0)
        {
            var recipients = await client.GetFromJsonAsync<PagedResultDto<RecipientDto>>(
                "api/Recipients?includeInactive=true&pageSize=200", ApiJsonOptions);
            var legacyRecipient = recipients?.Items.FirstOrDefault(r => r.Name == LegacyDataRecipientName);

            if (legacyRecipient != null)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                foreach (var row in rowsWithOut)
                {
                    var (materialId, arrivalQty) = inboundedMaterials[row.Barcode!];
                    if (row.Balance.HasValue && Math.Abs((arrivalQty - row.Out!.Value) - row.Balance.Value) > 0.01m)
                    {
                        skipped.Add(new MaterialImportSkipItem(row.Barcode!,
                            $"OUT ({row.Out}) + BALANCE ({row.Balance}) không khớp Q'TY ({arrivalQty}) — chưa tự xuất, kiểm tra lại rồi xuất tay ở màn Xuất hàng."));
                        continue;
                    }

                    var issueResp = await client.PostAsJsonAsync($"api/Materials/{materialId}/issue",
                        new { recipientId = legacyRecipient.RecipientId, qty = row.Out!.Value, userId });
                    if (issueResp.IsSuccessStatusCode)
                    {
                        issuedCount++;
                    }
                    else
                    {
                        var errBody = await issueResp.Content.ReadAsStringAsync();
                        skipped.Add(new MaterialImportSkipItem(row.Barcode!,
                            $"Tự xuất OUT ({row.Out}) thất bại — kiểm tra lại rồi xuất tay ở màn Xuất hàng. ({errBody})"));
                    }
                }
            }
            else
            {
                foreach (var row in rowsWithOut)
                {
                    skipped.Add(new MaterialImportSkipItem(row.Barcode!,
                        $"Không tìm thấy recipient \"{LegacyDataRecipientName}\" — chưa tự xuất OUT ({row.Out}), kiểm tra lại rồi xuất tay ở màn Xuất hàng."));
                }
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
            if (issuedCount > 0)
            {
                parts.Add(("importedAutoIssuedSuffix", new[] { issuedCount.ToString() }));
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
            // Chỉ mang chi tiết tối đa 500 dòng lỗi qua popup (số thật vẫn hiển thị qua ImportSkippedCount)
            // — file toàn dòng lỗi có thể lên vài nghìn, không cần liệt kê hết trong 1 popup.
            const int maxSkippedDetail = 500;
            TempData["ImportSkippedJson"] = JsonSerializer.Serialize(skipped.Take(maxSkippedDetail));
            TempData["ImportSkippedCount"] = skipped.Count;
            TempData["ImportInsertedCount"] = insertedCount;
            TempData["ImportTotalCount"] = parsedRows.Count;
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
            // Q'TY = tên cột mới PMC yêu cầu, A.Q'TY = tên cũ — giữ fallback cho file mẫu cũ đã tải trước đó.
            ArrivalQty = ParseDecimal(Get("Q'TY") ?? Get("A.Q'TY")),
            Unit = Get("UNIT"),
            FocFlag = Get("FOC/NON FOC") ?? Get("FOC"),
            ArrivalDate = ParseDate(Get("ATA (INPUT)") ?? Get("ATA")),
            CsCode = ParseInt(Get("CS_CODE")),
            Remark = Get("REMARK"),
            Barcode = Get("BARCODE"),
            Testing = ParseYesNo(Get("TESTING (YES/NO)") ?? Get("TESTING")),
            TestRequire = Get("TEST REQUIRE"),
            TestQty = Get("TEST Q'TY"),
            Category = Get("CATEGORY"),
            RequestOn = ParseDate(Get("REQUEST ON")),
            MatlType = Get("MAT'L TYPE"),
            Pic = Get("PIC"),
            PoDate = ParseDate(Get("PODATE")),
            Etd = ParseDate(Get("ETD")),
            OriginalPrice = ParseDecimal(Get("ORIGINAL PRICE")),
            PaymentPrice = ParseDecimal(Get("PAYMENT PRICE")),
            Amount = ParseDecimal(Get("AMOUNT")),
            RackNo = Get("RACK NO") ?? Get("RACK NO."),
            Out = ParseDecimal(Get("OUT")),
            Balance = ParseDecimal(Get("BALANCE")),
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
