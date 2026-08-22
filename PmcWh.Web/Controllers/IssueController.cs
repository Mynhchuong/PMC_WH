using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PmcWh.Web.Helpers;
using PmcWh.Web.Hubs;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class IssueController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<WarehouseHub> _hub;

    public IssueController(IHttpClientFactory httpClientFactory, IHubContext<WarehouseHub> hub)
    {
        _httpClientFactory = httpClientFactory;
        _hub = hub;
    }

    public async Task<IActionResult> Index(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3, int page = 1, int pageSize = 10)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var searchQuery = SearchFilterHelper.ToQueryString(field, q, field2, q2, field3, q3);

        var issuableTask = client.GetFromJsonAsync<PagedResultDto<MaterialListItem>>(
            $"api/Materials/issuable?page={page}&pageSize={pageSize}&{searchQuery}", ApiJsonOptions);
        var recipientsTask = client.GetFromJsonAsync<PagedResultDto<RecipientDto>>("api/Recipients", ApiJsonOptions);

        await Task.WhenAll(issuableTask, recipientsTask);
        var issuable = await issuableTask;

        var model = new IssueViewModel
        {
            IssuableItems = issuable?.Items ?? new List<MaterialListItem>(),
            Recipients = (await recipientsTask)?.Items ?? new List<RecipientDto>(),
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
                TotalCount = issuable?.TotalCount ?? 0,
                TotalPages = issuable?.TotalPages ?? 0,
                Controller = "Issue",
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
    public async Task<IActionResult> Scan(int materialId, int recipientId, decimal qty, string barcode)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync($"api/Materials/{materialId}/issue", new { recipientId, qty, userId });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("issuedSuccess", qty.ToString(), barcode);
            await _hub.Clients.All.SendAsync("warehouseChanged");
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            TempData["FlashError"] = problem?.Message ?? FlashHelper.Msg("issueFailFallback", barcode);
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
