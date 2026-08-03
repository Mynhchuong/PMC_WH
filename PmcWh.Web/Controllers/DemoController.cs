using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

// Controller tạm để test PaginationHelper/_Pagination.cshtml bằng data giả,
// vì chưa có Oracle thật để gọi QueryPagedAsync. Xoá khi có màn hình thật.
public class DemoController : Controller
{
    private const int DefaultPageSize = 10;

    public IActionResult Paging(int page = 1, int pageSize = DefaultPageSize)
    {
        var allItems = Enumerable.Range(1, 97).Select(i => new DemoItem
        {
            MaHang = $"SP{i:000}",
            TenHang = $"Hàng hoá mẫu số {i}",
            SoLuong = i * 3 % 200,
            NgayNhap = new DateTime(2026, 1, 1).AddDays(i),
        }).ToList();

        if (page < 1) page = 1;
        // pageSize <= 0 nghĩa là "Tất cả" — không phân trang.
        var effectivePageSize = pageSize <= 0 ? allItems.Count : pageSize;
        var totalPages = pageSize <= 0 ? 1 : (int)Math.Ceiling(allItems.Count / (double)effectivePageSize);
        if (page > totalPages) page = totalPages;

        var pagedItems = pageSize <= 0
            ? allItems
            : allItems.Skip((page - 1) * effectivePageSize).Take(effectivePageSize).ToList();

        var model = new DemoPagingViewModel
        {
            Items = pagedItems,
            Pagination = new PaginationViewModel
            {
                Page = page,
                TotalPages = totalPages,
                PageSize = pageSize <= 0 ? 0 : pageSize,
                TotalCount = allItems.Count,
                Controller = "Demo",
                Action = "Paging",
            },
        };

        return View(model);
    }
}
