using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PmcWh.Web.Helpers;
using PmcWh.Web.Hubs;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class InboundController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<WarehouseHub> _hub;

    public InboundController(IHttpClientFactory httpClientFactory, IHubContext<WarehouseHub> hub)
    {
        _httpClientFactory = httpClientFactory;
        _hub = hub;
    }

    /// <summary>Gộp chung Mới nhập (Staging) + Nhận lại (Returnable) thành 1 danh sách rồi phân
    /// trang ở tầng Web (Api trả nguyên 2 danh sách đầy đủ, không đổi shape response Api để tránh
    /// vỡ endpoint /returnable mà app Android có thể còn dùng) — giống cách BarcodeCollection/Detail
    /// đã làm, để bảng "Cần lên kệ" có chung 1 thanh cuộn + chọn số dòng/trang như mọi màn khác.</summary>
    public async Task<IActionResult> Index(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3, int page = 1, int pageSize = 10)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var searchQuery = SearchFilterHelper.ToQueryString(field, q, field2, q2, field3, q3);

        var stagingTask = client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(
            $"api/Materials?status=Staging&page=1&pageSize=1000&{searchQuery}", ApiJsonOptions);
        var returnableTask = client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(
            $"api/Materials/returnable?page=1&pageSize=1000&{searchQuery}", ApiJsonOptions);
        var locationsTask = client.GetFromJsonAsync<List<StorageLocationDto>>("api/StorageLocations", ApiJsonOptions);

        await Task.WhenAll(stagingTask, returnableTask, locationsTask);

        // Ưu tiên xử lý theo FIFO (liệu chờ lâu nhất trước) — cùng quy ước với Issuable/Returnable bên Api.
        var stagingItems = ((await stagingTask)?.Items ?? new List<MaterialListItem>())
            .OrderBy(m => m.ArrivalDate ?? DateTime.MaxValue)
            .ThenBy(m => m.CreatedAt)
            .Select(m => new InboundQueueItem { Material = m, Kind = "New", Outstanding = m.ArrivalQty ?? 0 });

        var returnableItems = ((await returnableTask)?.Items ?? new List<MaterialListItem>())
            .Select(m => new InboundQueueItem { Material = m, Kind = "Return", Outstanding = (m.ArrivalQty ?? 0) - (m.Balance ?? 0) });

        var allItems = stagingItems.Concat(returnableItems).ToList();
        if (page < 1) page = 1;
        var totalPages = pageSize > 0 ? (int)Math.Ceiling(allItems.Count / (double)pageSize) : 1;
        var pagedItems = pageSize > 0 ? allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList() : allItems;

        var model = new InboundViewModel
        {
            QueueItems = pagedItems,
            NewCount = stagingItems.Count(),
            ReturnCount = returnableItems.Count(),
            Locations = await locationsTask ?? new List<StorageLocationDto>(),
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
                TotalCount = allItems.Count,
                TotalPages = totalPages,
                Controller = "Inbound",
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
    public async Task<IActionResult> Scan(int materialId, int? locationId, string barcode, decimal? qty)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var client = _httpClientFactory.CreateClient("PmcApi");

        HttpResponseMessage response;
        string successMessage;
        string failMessage;

        if (qty.HasValue)
        {
            response = await client.PostAsJsonAsync($"api/Materials/{materialId}/return", new { locationId, qty = qty.Value, userId });
            successMessage = FlashHelper.Msg("returnedSuccess", qty.Value.ToString(), barcode);
            failMessage = FlashHelper.Msg("returnedFailFallback", barcode);
        }
        else
        {
            response = await client.PostAsJsonAsync($"api/Materials/{materialId}/inbound", new { locationId, userId });
            successMessage = FlashHelper.Msg("shelvedSuccess", barcode);
            failMessage = FlashHelper.Msg("shelvedFailFallback", barcode);
        }

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = successMessage;
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? failMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    private class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
