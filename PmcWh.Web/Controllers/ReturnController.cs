using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class ReturnController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public ReturnController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");

        var returnableTask = client.GetFromJsonAsync<List<MaterialListItem>>("api/Materials/returnable", ApiJsonOptions);
        var locationsTask = client.GetFromJsonAsync<List<StorageLocationDto>>("api/StorageLocations", ApiJsonOptions);

        await Task.WhenAll(returnableTask, locationsTask);

        var model = new ReturnViewModel
        {
            ReturnableItems = await returnableTask ?? new List<MaterialListItem>(),
            Locations = await locationsTask ?? new List<StorageLocationDto>(),
        };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(int materialId, int locationId, decimal qty, string barcode)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync($"api/Materials/{materialId}/return", new { locationId, qty, userId });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = $"Đã nhận lại {qty} cho barcode '{barcode}'.";
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? $"Không thể nhận lại barcode '{barcode}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
