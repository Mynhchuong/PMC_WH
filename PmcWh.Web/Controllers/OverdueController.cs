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

[Authorize(Roles = "Admin")]
public class OverdueController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<WarehouseHub> _hub;

    public OverdueController(IHttpClientFactory httpClientFactory, IHubContext<WarehouseHub> hub)
    {
        _httpClientFactory = httpClientFactory;
        _hub = hub;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var paged = await client.GetFromJsonAsync<PagedResultDto<OverdueIssuedItem>>(
            $"api/Materials/overdue-issued?page={page}&pageSize={pageSize}", ApiJsonOptions);

        var model = new OverdueViewModel
        {
            Items = paged?.Items ?? new List<OverdueIssuedItem>(),
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = paged?.TotalCount ?? 0,
                TotalPages = paged?.TotalPages ?? 0,
                Controller = "Overdue",
                Action = "Index",
                RouteValues = new Dictionary<string, string?>(),
            },
        };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(int materialId, string barcode)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync($"api/Materials/{materialId}/dispose", new { userId });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("overdueDisposedSuccess", barcode);
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? FlashHelper.Msg("disposeFailFallback", barcode);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Hủy nhiều liệu quá hạn cùng lúc (checkbox chọn nhiều ở Overdue/Index) — gọi tuần tự
    /// từng cái qua đúng API dispose (không có endpoint batch riêng bên Api), gộp lại 1 thông báo
    /// tổng kết thay vì spam nhiều toast, chỉ báo warehouseChanged 1 lần ở cuối.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDispose(int[] materialIds)
    {
        if (materialIds == null || materialIds.Length == 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var client = _httpClientFactory.CreateClient("PmcApi");

        var successCount = 0;
        foreach (var materialId in materialIds)
        {
            var response = await client.PostAsJsonAsync($"api/Materials/{materialId}/dispose", new { userId });
            if (response.IsSuccessStatusCode) successCount++;
        }

        var failCount = materialIds.Length - successCount;
        if (successCount > 0)
        {
            TempData["FlashSuccess"] = failCount > 0
                ? FlashHelper.Msg("bulkOverdueDisposedPartial", successCount.ToString(), materialIds.Length.ToString())
                : FlashHelper.Msg("bulkOverdueDisposedSuccess", successCount.ToString());
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            TempData["FlashError"] = FlashHelper.Msg("bulkOverdueDisposedFail");
        }

        return RedirectToAction(nameof(Index));
    }

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
