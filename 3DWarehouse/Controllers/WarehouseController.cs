using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Warehouse3D.Helpers;
using Warehouse3D.Models;
using Warehouse3D.Services;

namespace Warehouse3D.Controllers;

// Project demo/show riêng chỉ để trình chiếu Bản đồ kho 3D — API + Web gộp chung 1 project,
// controller này vừa trả View (Map) vừa trả JSON (LayoutData/LocationMaterials/Find/Dashboard)
// gọi thẳng OracleDataService, không qua HttpClient proxy như PmcWh.Web/PmcWh.Api tách rời.
//
// Route có {warehouseId} (vd /Warehouse/pmc/Map) — kho nào chạy cũng qua đúng 1 bộ action này,
// KHÔNG tạo Controller riêng cho từng kho. Xem Helpers/WarehouseRegistry.cs để biết cách thêm
// 1 kho mới (khai báo registry + set User Secrets), mọi route/JSON endpoint bên dưới tự nhận.
//
// SQL hiện hard-code theo đúng schema PMC (PMC_StorageLocations/PMC_Materials/...) — xem ghi chú
// trong WarehouseRegistry.cs về trường hợp kho mới có schema khác.
[Route("Warehouse/{warehouseId}/[action]")]
public class WarehouseController : Controller
{
    private readonly OracleDataServiceFactory _factory;

    public WarehouseController(OracleDataServiceFactory factory)
    {
        _factory = factory;
    }

    // Chặn kho không tồn tại / chưa active (IsActive=false, "sắp có") ngay từ đầu, trước khi động
    // tới OracleDataServiceFactory.Create (tránh cố resolve connection cho 1 kho chưa có config).
    private bool TryResolve(string warehouseId, out WarehouseDefinition warehouse, out OracleDataService db)
    {
        var found = WarehouseRegistry.Find(warehouseId);
        if (found is null || !found.IsActive)
        {
            warehouse = null!;
            db = null!;
            return false;
        }

        warehouse = found;
        db = _factory.Create(warehouse.Id);
        return true;
    }

    public async Task<IActionResult> Map(string warehouseId)
    {
        if (!TryResolve(warehouseId, out var warehouse, out var db))
        {
            return NotFound();
        }

        // Đọc ở _WarehouseMenu.cshtml (partial, dùng chung trong _Layout) để tô sáng đúng kho
        // đang xem trong menu — giống cách _SideNav.cshtml bên PmcWh.Web highlight mục đang chọn.
        ViewBag.CurrentWarehouseId = warehouse.Id;

        // Schema "generic" (WH_Racks/WH_RackLocations) có hình dạng dữ liệu khác hẳn PMC (1 tầng
        // nhiều ô, chưa có QR/barcode/dashboard) nên dùng 1 View + ViewModel riêng, không ép vào
        // WarehouseMapViewModel của PMC — xem ghi chú ở WarehouseRegistry.cs.
        if (warehouse.Schema == "generic")
        {
            return View("GenericMap", await BuildGenericMapModelAsync(warehouse, db));
        }

        return View(await BuildMapModelAsync(warehouse, db));
    }

    private async Task<WarehouseMapViewModel> BuildMapModelAsync(WarehouseDefinition warehouse, OracleDataService db)
    {
        var tiers = await LoadLayoutAsync(db);
        return new WarehouseMapViewModel
        {
            Warehouse = warehouse,
            Tiers = tiers,
            TotalTiers = tiers.Count,
            OccupiedTiers = tiers.Count(t => t.QrCount > 0),
            TotalQr = tiers.Sum(t => t.QrCount),
        };
    }

    // JS (warehouse-map.js) gọi lại định kỳ để cập nhật số mã QR mỗi ô.
    [HttpGet]
    public async Task<IActionResult> LayoutData(string warehouseId)
    {
        if (!TryResolve(warehouseId, out var warehouse, out var db) || warehouse.Schema != "pmc")
        {
            return NotFound();
        }

        return Json(await LoadLayoutAsync(db));
    }

    private async Task<GenericWarehouseMapViewModel> BuildGenericMapModelAsync(WarehouseDefinition warehouse, OracleDataService db)
    {
        var racks = await LoadGenericLayoutAsync(db, warehouse.Id);
        var allLocations = racks.SelectMany(r => r.Front.Concat(r.Back)).ToList();
        return new GenericWarehouseMapViewModel
        {
            Warehouse = warehouse,
            Racks = racks,
            TotalRacks = racks.Count,
            TotalLocations = allLocations.Count,
            FilledLocations = allLocations.Count(l => !string.IsNullOrEmpty(l.Code)),
        };
    }

    // JS (warehouse-map-generic.js) gọi lại định kỳ để cập nhật layout — cùng vai trò LayoutData
    // bên trên nhưng cho schema "generic" (WH_Racks/WH_RackLocations, xem WarehouseRegistry.cs).
    [HttpGet]
    public async Task<IActionResult> GenericLayoutData(string warehouseId)
    {
        if (!TryResolve(warehouseId, out var warehouse, out var db) || warehouse.Schema != "generic")
        {
            return NotFound();
        }

        return Json(await LoadGenericLayoutAsync(db, warehouse.Id));
    }

    private async Task<List<GenericRackDto>> LoadGenericLayoutAsync(OracleDataService db, string warehouseId)
    {
        var rackRows = await db.QueryAsync(
            @"SELECT RackId, RackOrder, Side, Label, Note, GapBefore
                FROM WH_Racks
               WHERE WarehouseId = :wid AND IsActive = 1
               ORDER BY RackOrder",
            new OracleParameter("wid", warehouseId));

        var locRows = await db.QueryAsync(
            @"SELECT RackId, Face, LevelNo, CellNo, LocationCode
                FROM WH_RackLocations
               WHERE WarehouseId = :wid
               ORDER BY RackId, Face, LevelNo, CellNo",
            new OracleParameter("wid", warehouseId));

        var locsByRack = locRows
            .GroupBy(r => r["RACKID"]!.ToString()!)
            .ToDictionary(g => g.Key, g => g.ToList());

        return rackRows.Select(r =>
        {
            var rackId = r["RACKID"]!.ToString()!;
            var locs = locsByRack.TryGetValue(rackId, out var l) ? l : new List<Dictionary<string, object?>>();

            List<GenericLocationDto> Side(string face) => locs
                .Where(x => string.Equals(x["FACE"]?.ToString(), face, StringComparison.OrdinalIgnoreCase))
                .Select(x => new GenericLocationDto
                {
                    LevelNo = Convert.ToInt32(x["LEVELNO"]),
                    CellNo = Convert.ToInt32(x["CELLNO"]),
                    Code = x["LOCATIONCODE"]?.ToString(),
                })
                .ToList();

            return new GenericRackDto
            {
                RackId = rackId,
                Order = Convert.ToInt32(r["RACKORDER"]),
                Side = r["SIDE"]?.ToString() ?? "L",
                Label = r["LABEL"]?.ToString(),
                Note = r["NOTE"]?.ToString(),
                GapBefore = Convert.ToInt32(r["GAPBEFORE"]),
                Front = Side("FRONT"),
                Back = Side("BACK"),
            };
        }).ToList();
    }

    private async Task<List<WarehouseTierDto>> LoadLayoutAsync(OracleDataService db)
    {
        var rows = await db.QueryAsync(
            @"SELECT l.LocationId, l.RackNo, l.LevelNo, l.Code, NVL(m.qr, 0) AS QrCount,
                     l.ManagerName, l.Purpose_Vi, l.Purpose_En
                FROM PMC_StorageLocations l
                LEFT JOIN (
                     SELECT CurrentLocationId, COUNT(*) AS qr
                       FROM PMC_Materials
                      WHERE IsArchived = 0
                        AND CurrentLocationId IS NOT NULL
                        AND Status IN ('InStock', 'PartiallyIssued')
                      GROUP BY CurrentLocationId
                ) m ON m.CurrentLocationId = l.LocationId
               WHERE l.IsActive = 1
               ORDER BY l.RackNo, l.LevelNo");

        return rows.Select(r => new WarehouseTierDto
        {
            LocationId = Convert.ToInt32(r["LOCATIONID"]),
            RackNo = Convert.ToInt32(r["RACKNO"]),
            LevelNo = Convert.ToInt32(r["LEVELNO"]),
            Code = r["CODE"]?.ToString() ?? string.Empty,
            QrCount = Convert.ToInt32(r["QRCOUNT"]),
            ManagerName = r["MANAGERNAME"]?.ToString(),
            PurposeVi = r["PURPOSE_VI"]?.ToString(),
            PurposeEn = r["PURPOSE_EN"]?.ToString(),
        }).ToList();
    }

    // Danh sách mã QR (cây liệu) đang nằm ở 1 ô kệ — phân trang, JS gọi khi click 1 tầng.
    [HttpGet]
    public async Task<IActionResult> LocationMaterials(string warehouseId, int id, int page = 1)
    {
        if (!TryResolve(warehouseId, out var warehouse, out var db) || warehouse.Schema != "pmc")
        {
            return NotFound();
        }

        const string innerSql =
            @"SELECT MaterialId, Barcode, Dev, Model, SizeSpec, Balance, Unit
                FROM PMC_Materials
               WHERE IsArchived = 0 AND CurrentLocationId = :locId
                 AND Status IN ('InStock', 'PartiallyIssued')
               ORDER BY Barcode";

        var paged = await db.QueryPagedAsync(innerSql, page, 8, new OracleParameter("locId", id));

        var items = paged.Items.Select(r => new LocationMaterialDto
        {
            MaterialId = Convert.ToInt32(r["MATERIALID"]),
            Barcode = r["BARCODE"]?.ToString() ?? string.Empty,
            Dev = r["DEV"]?.ToString(),
            Model = r["MODEL"]?.ToString(),
            SizeSpec = r["SIZESPEC"]?.ToString(),
            Balance = r["BALANCE"] != null ? Convert.ToDecimal(r["BALANCE"]) : null,
            Unit = r["UNIT"]?.ToString(),
        }).ToList();

        return Json(new PagedResult<LocationMaterialDto>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    // Tìm barcode đang trong kho → trả ô kệ chứa nó + Ordinal để nhảy đúng trang.
    [HttpGet]
    public async Task<IActionResult> Find(string warehouseId, string barcode)
    {
        if (!TryResolve(warehouseId, out var warehouse, out var db) || warehouse.Schema != "pmc")
        {
            return NotFound();
        }

        var rows = (await db.QueryAsync(
            @"SELECT l.LocationId, l.RackNo, l.LevelNo, l.Code,
                     (SELECT COUNT(*) FROM PMC_Materials m2
                       WHERE m2.CurrentLocationId = m.CurrentLocationId
                         AND m2.IsArchived = 0 AND m2.Status IN ('InStock','PartiallyIssued')
                         AND m2.Barcode <= m.Barcode) AS Ordinal
                FROM PMC_Materials m
                JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0 AND UPPER(m.Barcode) = UPPER(:bc)
                 AND m.Status IN ('InStock', 'PartiallyIssued')",
            new OracleParameter("bc", barcode ?? string.Empty))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = $"Không tìm thấy barcode '{barcode}' trong kho (có thể đã xuất/hủy)." });
        }

        var r = rows[0];
        return Json(new FindLocationDto
        {
            LocationId = Convert.ToInt32(r["LOCATIONID"]),
            RackNo = Convert.ToInt32(r["RACKNO"]),
            LevelNo = Convert.ToInt32(r["LEVELNO"]),
            Code = r["CODE"]?.ToString() ?? string.Empty,
            Ordinal = Convert.ToInt32(r["ORDINAL"]),
        });
    }

    // Dữ liệu cho banner TV: tổng quan toàn kho + 5 lượt lên kệ / xuất kho gần nhất.
    [HttpGet]
    public async Task<IActionResult> Dashboard(string warehouseId)
    {
        if (!TryResolve(warehouseId, out var warehouse, out var db) || warehouse.Schema != "pmc")
        {
            return NotFound();
        }

        var summaryRows = (await db.QueryAsync(
            @"SELECT
                (SELECT COUNT(*) FROM PMC_Materials
                  WHERE IsArchived = 0 AND Status IN ('InStock', 'PartiallyIssued')) AS TotalInStock,
                (SELECT COUNT(*) FROM PMC_StockMovements
                  WHERE MovementType = 'IssueToWorkshop' AND TRUNC(OccurredAt) = TRUNC(SYSDATE)) AS IssuedToday,
                (SELECT COUNT(*) FROM PMC_StockMovements
                  WHERE MovementType = 'Inbound' AND TRUNC(OccurredAt) = TRUNC(SYSDATE)) AS InboundToday
              FROM DUAL")).ToList();

        var s = summaryRows[0];
        var summary = new WarehouseSummaryDto
        {
            TotalInStock = Convert.ToInt32(s["TOTALINSTOCK"]),
            IssuedToday = Convert.ToInt32(s["ISSUEDTODAY"]),
            InboundToday = Convert.ToInt32(s["INBOUNDTODAY"]),
        };

        var inboundRows = await db.QueryAsync(
            @"SELECT * FROM (
                  SELECT mv.MaterialId, m.Barcode, m.Dev, m.Model, mv.Qty, m.Unit, l.Code AS LocationCode, mv.OccurredAt
                    FROM PMC_StockMovements mv
                    JOIN PMC_Materials m ON m.MaterialId = mv.MaterialId
                    LEFT JOIN PMC_StorageLocations l ON l.LocationId = mv.LocationId
                   WHERE mv.MovementType = 'Inbound' AND m.IsArchived = 0
                   ORDER BY mv.OccurredAt DESC
              ) WHERE ROWNUM <= 5");

        var issueRows = await db.QueryAsync(
            @"SELECT * FROM (
                  SELECT mv.MaterialId, m.Barcode, m.Dev, m.Model, mv.Qty, m.Unit, r.Name AS RecipientName, mv.OccurredAt
                    FROM PMC_StockMovements mv
                    JOIN PMC_Materials m ON m.MaterialId = mv.MaterialId
                    LEFT JOIN PMC_Recipients r ON r.RecipientId = mv.RecipientId
                   WHERE mv.MovementType = 'IssueToWorkshop' AND m.IsArchived = 0
                   ORDER BY mv.OccurredAt DESC
              ) WHERE ROWNUM <= 5");

        return Json(new WarehouseDashboardDto
        {
            Summary = summary,
            RecentInbound = inboundRows.Select(MapActivity).ToList(),
            RecentIssue = issueRows.Select(MapActivity).ToList(),
        });
    }

    private static RecentActivityDto MapActivity(Dictionary<string, object?> row) => new()
    {
        MaterialId = Convert.ToInt32(row["MATERIALID"]),
        Barcode = row["BARCODE"]?.ToString() ?? string.Empty,
        Dev = row["DEV"]?.ToString(),
        Model = row["MODEL"]?.ToString(),
        Qty = Convert.ToDecimal(row["QTY"]),
        Unit = row["UNIT"]?.ToString(),
        LocationCode = row.ContainsKey("LOCATIONCODE") ? row["LOCATIONCODE"]?.ToString() : null,
        RecipientName = row.ContainsKey("RECIPIENTNAME") ? row["RECIPIENTNAME"]?.ToString() : null,
        OccurredAt = Convert.ToDateTime(row["OCCURREDAT"]),
    };
}
