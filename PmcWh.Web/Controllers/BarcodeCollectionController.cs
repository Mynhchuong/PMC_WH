using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Helpers;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

/// <summary>
/// "Thu thập Barcode" — quét trên Android, xem/xoá/xuất Excel ở đây. Đồng bộ 2 chiều qua chung 1
/// Api (PmcWh.Api BarcodeCollectionController), không lưu gì riêng ở Web. Không giới hạn Admin —
/// mọi nhân viên đã đăng nhập dùng được, giống Nhập/Xuất kho.
/// </summary>
public class BarcodeCollectionController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;

    public BarcodeCollectionController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var lists = await client.GetFromJsonAsync<List<BarcodeListDto>>("api/BarcodeCollection/lists", ApiJsonOptions)
                    ?? new List<BarcodeListDto>();

        var model = new BarcodeCollectionIndexViewModel { Lists = lists };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBarcodeListViewModel newList)
    {
        if (!ModelState.IsValid)
        {
            return await RenderIndexWithErrors(newList);
        }

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync("api/BarcodeCollection/lists", new { name = newList.Name, userId });

        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            ModelState.AddModelError(string.Empty, problem?.Message ?? "Không thể tạo list.");
            return await RenderIndexWithErrors(newList);
        }

        var created = await response.Content.ReadFromJsonAsync<BarcodeListDto>(ApiJsonOptions);
        TempData["FlashSuccess"] = FlashHelper.Msg("createdBarcodeListSuccess", newList.Name);
        return RedirectToAction(nameof(Detail), new { id = created?.ListId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteList(int listId, string name)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.DeleteAsync($"api/BarcodeCollection/lists/{listId}");

        TempData[response.IsSuccessStatusCode ? "FlashSuccess" : "FlashError"] = response.IsSuccessStatusCode
            ? FlashHelper.Msg("deletedBarcodeListSuccess", name)
            : FlashHelper.Msg("deleteBarcodeListFailFallback", name);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var lists = await client.GetFromJsonAsync<List<BarcodeListDto>>("api/BarcodeCollection/lists", ApiJsonOptions) ?? new();
        var list = lists.FirstOrDefault(l => l.ListId == id);
        if (list == null)
        {
            return NotFound();
        }

        var items = await client.GetFromJsonAsync<List<BarcodeListItemDto>>($"api/BarcodeCollection/lists/{id}/items", ApiJsonOptions)
                    ?? new List<BarcodeListItemDto>();

        var model = new BarcodeCollectionDetailViewModel { ListId = id, ListName = list.Name, Items = items };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int listId, int itemId, string barcode)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.DeleteAsync($"api/BarcodeCollection/lists/{listId}/items/{itemId}");

        TempData[response.IsSuccessStatusCode ? "FlashSuccess" : "FlashError"] = response.IsSuccessStatusCode
            ? FlashHelper.Msg("deletedBarcodeItemSuccess", barcode)
            : FlashHelper.Msg("deleteBarcodeItemFailFallback", barcode);

        return RedirectToAction(nameof(Detail), new { id = listId });
    }

    public async Task<IActionResult> ExportExcel(int id)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var lists = await client.GetFromJsonAsync<List<BarcodeListDto>>("api/BarcodeCollection/lists", ApiJsonOptions) ?? new();
        var list = lists.FirstOrDefault(l => l.ListId == id);
        if (list == null)
        {
            return NotFound();
        }

        var items = await client.GetFromJsonAsync<List<BarcodeListItemDto>>($"api/BarcodeCollection/lists/{id}/items", ApiJsonOptions)
                    ?? new List<BarcodeListItemDto>();

        var headers = new List<string> { "BARCODE", "SỐ LẦN QUÉT" };
        var rows = items
            .Select(i => (IReadOnlyList<object?>)new List<object?> { i.Barcode, i.ScanCount })
            .Append(new List<object?> { "TỔNG CỘNG", items.Sum(i => i.ScanCount) })
            .ToList();

        var bytes = ExcelHelper.WriteRows(headers, rows);
        var safeName = string.Join("_", list.Name.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private async Task<IActionResult> RenderIndexWithErrors(CreateBarcodeListViewModel newList)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var lists = await client.GetFromJsonAsync<List<BarcodeListDto>>("api/BarcodeCollection/lists", ApiJsonOptions) ?? new();

        var model = new BarcodeCollectionIndexViewModel { Lists = lists, NewList = newList };
        return View(nameof(Index), model);
    }

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
