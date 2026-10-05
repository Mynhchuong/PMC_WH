using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using Warehouse3D.Helpers;
using Warehouse3D.Models;
using Warehouse3D.Services;

namespace Warehouse3D.Controllers;

[Route("Warehouse/{warehouseId}/MaterialReport/[action]")]
public class MaterialReportController : Controller
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly OracleDataServiceFactory _factory;
    private readonly IMemoryCache _cache;

    public MaterialReportController(OracleDataServiceFactory factory, IMemoryCache cache)
    {
        _factory = factory;
        _cache = cache;
    }

    private async Task<List<IReadOnlyList<object?>>> LoadBarcodesAsync(string warehouseId, OracleDataService db, bool refresh)
    {
        var key = $"mr:barcodes:{warehouseId}";
        if (!refresh && _cache.TryGetValue(key, out List<IReadOnlyList<object?>>? hit) && hit is not null)
        {
            return hit;
        }

        var rows = (await db.QueryAsync(BarcodeSql)).Select(BarcodeRow).ToList();
        _cache.Set(key, rows, CacheTtl);
        return rows;
    }

    private async Task<List<IReadOnlyList<object?>>> LoadCuttingAsync(string warehouseId, OracleDataService db, string cutDate, bool refresh)
    {
        var key = $"mr:cutting:{warehouseId}:{cutDate}";
        if (!refresh && _cache.TryGetValue(key, out List<IReadOnlyList<object?>>? hit) && hit is not null)
        {
            return hit;
        }

        var rows = (await db.QueryAsync(CuttingSql, new OracleParameter("cutDate", cutDate))).Select(CuttingRow).ToList();
        _cache.Set(key, rows, CacheTtl);
        return rows;
    }

    private static List<IReadOnlyList<object?>> FilterBarcodes(List<IReadOnlyList<object?>> rows, string? q)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return rows;
        }

        return rows.Where(r => Contains(r[0], term) || Contains(r[1], term) || Contains(r[3], term)).ToList();
    }

    private static bool Contains(object? v, string term) =>
        v is not null && Convert.ToString(v, CultureInfo.InvariantCulture)!.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static object PageOf(List<IReadOnlyList<object?>> rows, int page, int pageSize)
    {
        if (pageSize < 1) pageSize = 50;
        var totalPages = (int)Math.Ceiling(rows.Count / (double)pageSize);
        page = Math.Clamp(page, 1, Math.Max(totalPages, 1));
        return new
        {
            available = true,
            page,
            totalPages,
            totalCount = rows.Count,
            rows = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Select(Text)),
        };
    }

    private bool TryResolve(string warehouseId, out WarehouseDefinition warehouse, out OracleDataService db)
    {
        var found = WarehouseRegistry.Find(warehouseId);
        if (found is null || !found.IsActive || found.Schema != "generic")
        {
            warehouse = null!;
            db = null!;
            return false;
        }

        warehouse = found;
        db = _factory.Create(warehouseId);
        return true;
    }

    [HttpGet]
    public IActionResult Index(string warehouseId)
    {
        if (!TryResolve(warehouseId, out var warehouse, out _))
        {
            return NotFound();
        }

        ViewBag.CurrentWarehouseId = warehouse.Id;
        return View(warehouse);
    }

    private const string BarcodeSql =
        @"SELECT B.I_BARCODE, C.SEGMENT1, C.DESCRIPTION, B.I_AREA, B.Q_QTY, B.VENDOR_ID, D.VENDOR_NAME,
                 B.C_PONUM, B.C_STYLE, B.CUT_ORIGINAL, B.CREATE_DATE
            FROM MES.MTL_BAR_BARCODE@inf_m_e B, MTL_SYSTEM_ITEMS_B@inf_m_e C, po_vendors@inf_m_e D
           WHERE B.ITEM_ID = C.INVENTORY_ITEM_ID(+)
             AND B.VENDOR_ID = D.VENDOR_ID(+)
             AND B.I_STATUS = 'Y'
             AND NOT EXISTS (SELECT 1 FROM MES.MTL_BAR_SCANNING@inf_m_e S
                              WHERE S.I_BARCODE = B.I_BARCODE AND S.I_STATUS = 'O')
           ORDER BY C.SEGMENT1, B.I_AREA, B.I_BARCODE";

    private const string CuttingSql =
        @"SELECT C.SEGMENT1, C.DESCRIPTION, B.I_AREA, B.VENDOR_ID, D.VENDOR_NAME, B.CUT_ORIGINAL,
                 COUNT(*) AS BARCODE_CNT, SUM(B.Q_QTY) AS TTL_QTY
            FROM MES.MTL_BAR_BARCODE@inf_m_e B, MTL_SYSTEM_ITEMS_B@inf_m_e C, po_vendors@inf_m_e D
           WHERE B.ITEM_ID = C.INVENTORY_ITEM_ID
             AND B.VENDOR_ID = D.VENDOR_ID
             AND B.I_STATUS = 'Y'
             AND B.CUT_ORIGINAL = :cutDate
           GROUP BY C.SEGMENT1, C.DESCRIPTION, B.I_AREA, B.VENDOR_ID, D.VENDOR_NAME, B.CUT_ORIGINAL
           ORDER BY C.SEGMENT1, B.I_AREA";

    private static string CutDate(string? date) =>
        DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("yyyy-MM-dd")
            : DateTime.Now.ToString("yyyy-MM-dd");

    private static string Text(object? v) => v switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static IReadOnlyList<object?> BarcodeRow(Dictionary<string, object?> r) => new object?[]
    {
        r["I_BARCODE"], r["SEGMENT1"], r["DESCRIPTION"], r["I_AREA"], r["Q_QTY"], r["VENDOR_ID"], r["VENDOR_NAME"],
        r["C_PONUM"], r["C_STYLE"], r["CUT_ORIGINAL"], r["CREATE_DATE"],
    };

    private static IReadOnlyList<object?> CuttingRow(Dictionary<string, object?> r) => new object?[]
    {
        r["SEGMENT1"], r["DESCRIPTION"], r["I_AREA"], r["VENDOR_ID"], r["VENDOR_NAME"], r["CUT_ORIGINAL"],
        r["BARCODE_CNT"], r["TTL_QTY"],
    };

    private static readonly string[] BarcodeHeaders =
        { "Barcode", "Mã hàng", "Mô tả", "Vị trí", "Số lượng", "Mã NCC", "NCC", "PO", "Style", "Lịch cắt", "Ngày tạo" };

    private static readonly string[] CuttingHeaders =
        { "Mã hàng", "Mô tả", "Vị trí", "Mã NCC", "NCC", "Lịch cắt", "Số barcode", "Tổng SL" };

    [HttpGet]
    public async Task<IActionResult> BarcodesData(string warehouseId, string? q, int page = 1, int pageSize = 50, bool refresh = false)
    {
        if (!TryResolve(warehouseId, out _, out var db))
        {
            return NotFound();
        }

        try
        {
            var rows = FilterBarcodes(await LoadBarcodesAsync(warehouseId, db, refresh), q);
            return Json(PageOf(rows, page, pageSize));
        }
        catch
        {
            return Json(new { available = false });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CuttingData(string warehouseId, string? date, int page = 1, int pageSize = 50, bool refresh = false)
    {
        if (!TryResolve(warehouseId, out _, out var db))
        {
            return NotFound();
        }

        try
        {
            var rows = await LoadCuttingAsync(warehouseId, db, CutDate(date), refresh);
            return Json(PageOf(rows, page, pageSize));
        }
        catch
        {
            return Json(new { available = false });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportScannedInNotOut(string warehouseId, string? q)
    {
        if (!TryResolve(warehouseId, out _, out var db))
        {
            return NotFound();
        }

        var rows = FilterBarcodes(await LoadBarcodesAsync(warehouseId, db, false), q);
        var bytes = ExcelExport.Write("Ton barcode", BarcodeHeaders, rows);
        return File(bytes, ExcelExport.ContentType, $"barcode_scan_in_chua_out_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportCutting(string warehouseId, string? date)
    {
        if (!TryResolve(warehouseId, out _, out var db))
        {
            return NotFound();
        }

        var cutDate = CutDate(date);
        var rows = await LoadCuttingAsync(warehouseId, db, cutDate, false);
        var bytes = ExcelExport.Write("Lich cat", CuttingHeaders, rows);
        return File(bytes, ExcelExport.ContentType, $"lich_cat_{cutDate.Replace("-", string.Empty)}.xlsx");
    }
}
