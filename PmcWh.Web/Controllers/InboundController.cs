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

    public async Task<IActionResult> Index(string? field, string? q, string? field2, string? q2, string? field3, string? q3)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var searchQuery = SearchFilterHelper.ToQueryString(field, q, field2, q2, field3, q3);

        var stagingTask = client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(
            $"api/Materials?status=Staging&page=1&pageSize=500&{searchQuery}", ApiJsonOptions);
        var returnableTask = client.GetFromJsonAsync<List<MaterialListItem>>($"api/Materials/returnable?{searchQuery}", ApiJsonOptions);
        var locationsTask = client.GetFromJsonAsync<List<StorageLocationDto>>("api/StorageLocations", ApiJsonOptions);

        await Task.WhenAll(stagingTask, returnableTask, locationsTask);

        // Ưu tiên xử lý theo FIFO (liệu chờ lâu nhất trước) — cùng quy ước với Issuable/Returnable bên Api.
        var stagingItems = ((await stagingTask)?.Items ?? new List<MaterialListItem>())
            .OrderBy(m => m.ArrivalDate ?? DateTime.MaxValue)
            .ThenBy(m => m.CreatedAt)
            .ToList();

        var model = new InboundViewModel
        {
            StagingItems = stagingItems,
            ReturnableItems = await returnableTask ?? new List<MaterialListItem>(),
            Locations = await locationsTask ?? new List<StorageLocationDto>(),
            Field = field,
            Q = q,
            Field2 = field2,
            Q2 = q2,
            Field3 = field3,
            Q3 = q3,
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
