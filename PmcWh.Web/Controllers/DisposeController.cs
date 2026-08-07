using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var items = await client.GetFromJsonAsync<List<MaterialListItem>>("api/Materials/disposable", ApiJsonOptions);

        var model = new DisposeViewModel { DisposableItems = items ?? new List<MaterialListItem>() };

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
            TempData["FlashSuccess"] = $"Đã hủy barcode '{barcode}'.";
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? $"Không thể hủy barcode '{barcode}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
