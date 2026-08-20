using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
    // Bản trong app cho nhân viên đã đăng nhập — dùng _Layout bình thường (có sidebar).
    public async Task<IActionResult> Map()
    {
        return View(await BuildMapModelAsync());
    }

    // Bản kiosk/TV công khai — không cần đăng nhập, dùng _KioskLayout riêng (Map2.cshtml,
    // không sidebar). Cùng dữ liệu với Map(), chỉ khác view/layout.
    [AllowAnonymous]
    public async Task<IActionResult> Map2()
    {
        return View(await BuildMapModelAsync());
    }

    private async Task<WarehouseMapViewModel> BuildMapModelAsync()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var tiers = await client.GetFromJsonAsync<List<WarehouseTier>>("api/Warehouse/layout", ApiJsonOptions)
                    ?? new List<WarehouseTier>();

        return new WarehouseMapViewModel
        {
            Tiers = tiers,
            TotalTiers = tiers.Count,
            OccupiedTiers = tiers.Count(t => t.QrCount > 0),
            TotalQr = tiers.Sum(t => t.QrCount),
        };
    }

    // Proxy: toàn bộ layout + số mã QR mỗi ô (JSON) — client gọi lại khi có realtime "warehouseChanged".
    // [AllowAnonymous] vì Map2 (kiosk, không đăng nhập) cũng gọi các proxy này — đều chỉ đọc, không
    // có action nào sửa/ghi dữ liệu hay lộ thông tin nhạy cảm (giá cả, người dùng...).
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> LayoutData()
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var resp = await client.GetAsync("api/Warehouse/layout");
        return await PassThroughAsync(resp);
    }

    // Proxy: danh sách mã QR trong 1 ô kệ (phân trang) — JS gọi same-origin.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> LocationMaterials(int id, int page = 1)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var resp = await client.GetAsync($"api/Warehouse/location/{id}/materials?page={page}&pageSize=8");
        return await PassThroughAsync(resp);
    }

    // Proxy: tìm barcode → trả ô kệ + Ordinal (hoặc 404 kèm message).
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Find(string barcode)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var resp = await client.GetAsync($"api/Warehouse/find?barcode={Uri.EscapeDataString(barcode ?? string.Empty)}");
        return await PassThroughAsync(resp);
    }

    // Proxy: dữ liệu màn hình giám sát TV (tổng quan + 5 lượt lên kệ / xuất kho gần nhất).
    [HttpGet]
    [AllowAnonymous]
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
