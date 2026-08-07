using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class WarehouseController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;

    public WarehouseController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // Bản đồ kho 3D (P5.1): dựng kệ từ dữ liệu thật, tô màu trống/có hàng theo số mã QR mỗi ô.
    public async Task<IActionResult> Map()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var tiers = await client.GetFromJsonAsync<List<WarehouseTier>>("api/Warehouse/layout", ApiJsonOptions)
                    ?? new List<WarehouseTier>();

        var model = new WarehouseMapViewModel
        {
            Tiers = tiers,
            TotalTiers = tiers.Count,
            OccupiedTiers = tiers.Count(t => t.QrCount > 0),
            TotalQr = tiers.Sum(t => t.QrCount),
        };
        return View(model);
    }

    // Proxy: toàn bộ layout + số mã QR mỗi ô (JSON) — client gọi lại khi có realtime "warehouseChanged".
    [HttpGet]
    public async Task<IActionResult> LayoutData()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var resp = await client.GetAsync("api/Warehouse/layout");
        return await PassThroughAsync(resp);
    }

    // Proxy: danh sách mã QR trong 1 ô kệ (phân trang) — JS gọi same-origin.
    [HttpGet]
    public async Task<IActionResult> LocationMaterials(int id, int page = 1)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var resp = await client.GetAsync($"api/Warehouse/location/{id}/materials?page={page}&pageSize=8");
        return await PassThroughAsync(resp);
    }

    // Proxy: tìm barcode → trả ô kệ + Ordinal (hoặc 404 kèm message).
    [HttpGet]
    public async Task<IActionResult> Find(string barcode)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var resp = await client.GetAsync($"api/Warehouse/find?barcode={Uri.EscapeDataString(barcode ?? string.Empty)}");
        return await PassThroughAsync(resp);
    }

    // Proxy: dữ liệu màn hình giám sát TV (tổng quan + 5 lượt lên kệ / xuất kho gần nhất).
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var resp = await client.GetAsync("api/Warehouse/dashboard");
        return await PassThroughAsync(resp);
    }

    // Trả nguyên body JSON + status code từ Api (giữ cả 404 kèm {message}).
    private static async Task<IActionResult> PassThroughAsync(HttpResponseMessage resp)
    {
        var json = await resp.Content.ReadAsStringAsync();
        return new ContentResult { Content = json, ContentType = "application/json", StatusCode = (int)resp.StatusCode };
    }
}
