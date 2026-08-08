using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Helpers;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

[Authorize(Roles = "Admin")]
public class RecipientsController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public RecipientsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var paged = await client.GetFromJsonAsync<PagedResultDto<RecipientDto>>(
            $"api/Recipients?includeInactive=true&page={page}&pageSize={pageSize}", ApiJsonOptions);

        var model = new RecipientListViewModel { Items = paged?.Items ?? new List<RecipientDto>() };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRecipientViewModel newRecipient)
    {
        if (!ModelState.IsValid)
        {
            return await RenderIndexWithErrors(newRecipient);
        }

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync("api/Recipients", newRecipient);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            ModelState.AddModelError(string.Empty, problem?.Message ?? "Không thể tạo nơi nhận.");
            return await RenderIndexWithErrors(newRecipient);
        }

        TempData["FlashSuccess"] = FlashHelper.Msg("addedRecipientSuccess", newRecipient.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int recipientId, string name)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsync($"api/Recipients/{recipientId}/toggle-active", null);

        TempData[response.IsSuccessStatusCode ? "FlashSuccess" : "FlashError"] = response.IsSuccessStatusCode
            ? FlashHelper.Msg("toggledRecipientSuccess", name)
            : FlashHelper.Msg("toggleRecipientFailFallback", name);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RenderIndexWithErrors(CreateRecipientViewModel newRecipient)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var paged = await client.GetFromJsonAsync<PagedResultDto<RecipientDto>>("api/Recipients?includeInactive=true", ApiJsonOptions);

        var model = new RecipientListViewModel
        {
            Items = paged?.Items ?? new List<RecipientDto>(),
            NewRecipient = newRecipient,
        };
        return View(nameof(Index), model);
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
