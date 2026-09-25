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
public class DisposeController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<WarehouseHub> _hub;

    public DisposeController(IHttpClientFactory httpClientFactory, IHubContext<WarehouseHub> hub)
    {
        _httpClientFactory = httpClientFactory;
        _hub = hub;
    }

    public async Task<IActionResult> Index(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3, int page = 1, int pageSize = 10)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var searchQuery = SearchFilterHelper.ToQueryString(field, q, field2, q2, field3, q3);
        var paged = await client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(
            $"api/Materials/disposable?page={page}&pageSize={pageSize}&{searchQuery}", ApiJsonOptions);

        var model = new DisposeViewModel
        {
            DisposableItems = paged?.Items ?? new List<MaterialListItem>(),
            Field = field,
            Q = q,
            Field2 = field2,
            Q2 = q2,
            Field3 = field3,
            Q3 = q3,
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = paged?.TotalCount ?? 0,
                TotalPages = paged?.TotalPages ?? 0,
                Controller = "Dispose",
                Action = "Index",
                RouteValues = new Dictionary<string, string?>
                {
                    ["field"] = field,
                    ["q"] = q,
                    ["field2"] = field2,
                    ["q2"] = q2,
                    ["field3"] = field3,
                    ["q3"] = q3,
                },
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
            TempData["FlashSuccess"] = FlashHelper.Msg("disposedSuccess", barcode);
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? FlashHelper.Msg("disposeFailFallback", barcode);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Hủy nhiều liệu cùng lúc (checkbox chọn nhiều ở Dispose/Index, nút "Hủy đã chọn") —
    /// cùng pattern với MaterialsController.BulkDelete: gọi tuần tự từng cái qua API dispose thật,
    /// gộp lại 1 thông báo tổng kết, chỉ báo warehouseChanged 1 lần ở cuối.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDispose(int[] materialIds, string? returnUrl)
    {
        if (materialIds == null || materialIds.Length == 0)
        {
            return RedirectToLocal(returnUrl);
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
                ? FlashHelper.Msg("bulkDisposedPartial", successCount.ToString(), materialIds.Length.ToString())
                : FlashHelper.Msg("bulkDisposedSuccess", successCount.ToString());
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            TempData["FlashError"] = FlashHelper.Msg("bulkDisposedFail");
        }

        return RedirectToLocal(returnUrl);
    }

    /// <summary>Redirect an toàn tới URL do client gửi lên (returnUrl) — chỉ chấp nhận local path,
    /// tránh open-redirect nếu returnUrl bị chỉnh thành 1 domain khác. Cùng pattern với
    /// MaterialsController.RedirectToLocal.</summary>
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
