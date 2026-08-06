using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class InboundController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public InboundController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");

        var stagingTask = client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(
            "api/Materials?status=Staging&page=1&pageSize=500", ApiJsonOptions);
        var locationsTask = client.GetFromJsonAsync<List<StorageLocationDto>>("api/StorageLocations", ApiJsonOptions);

        await Task.WhenAll(stagingTask, locationsTask);

        var model = new InboundViewModel
        {
            StagingItems = (await stagingTask)?.Items ?? new List<MaterialListItem>(),
            Locations = await locationsTask ?? new List<StorageLocationDto>(),
        };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(int materialId, int locationId, string barcode)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync($"api/Materials/{materialId}/inbound", new { locationId, userId });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = $"Đã lên kệ barcode '{barcode}'.";
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? $"Không thể lên kệ barcode '{barcode}'.";
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
