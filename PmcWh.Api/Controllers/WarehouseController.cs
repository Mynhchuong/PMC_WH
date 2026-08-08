using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly OracleDataService _db;

    public WarehouseController(OracleDataService db)
    {
        _db = db;
    }

    /// <summary>
    /// Toàn bộ 198 ô kệ (theo layout thật) kèm số mã QR đang có trong kho ở mỗi ô — dùng dựng
    /// bản đồ kho 3D. LEFT JOIN sang PMC_Materials đếm theo CurrentLocationId, chỉ tính liệu
    /// còn trong kho (InStock/PartiallyIssued, chưa xoá mềm). Ô không có liệu → QrCount = 0.
    /// </summary>
    [HttpGet("layout")]
    public async Task<ActionResult<List<WarehouseTierDto>>> Layout()
    {
        var rows = await _db.QueryAsync(
            @"SELECT l.LocationId, l.RackNo, l.LevelNo, l.Code, NVL(m.qr, 0) AS QrCount
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

        var items = rows.Select(r => new WarehouseTierDto
        {
            LocationId = Convert.ToInt32(r["LOCATIONID"]),
            RackNo = Convert.ToInt32(r["RACKNO"]),
            LevelNo = Convert.ToInt32(r["LEVELNO"]),
            Code = r["CODE"]?.ToString() ?? string.Empty,
            QrCount = Convert.ToInt32(r["QRCOUNT"]),
        }).ToList();

        return Ok(items);
    }

    /// <summary>Danh sách mã QR (cây liệu) đang nằm ở 1 ô kệ — phân trang. Chỉ liệu còn trong kho.</summary>
    [HttpGet("location/{id:int}/materials")]
    public async Task<ActionResult<PagedResult<LocationMaterialDto>>> LocationMaterials(int id, int page = 1, int pageSize = 8)
    {
        const string innerSql =
            @"SELECT MaterialId, Barcode, Dev, Model, SizeSpec, Balance, Unit
                FROM PMC_Materials
               WHERE IsArchived = 0 AND CurrentLocationId = :locId
                 AND Status IN ('InStock', 'PartiallyIssued')
               ORDER BY Barcode";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize, new OracleParameter("locId", id));

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

        return Ok(new PagedResult<LocationMaterialDto>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    /// <summary>Tìm barcode đang trong kho → trả ô kệ chứa nó + Ordinal (thứ tự trong ô) để nhảy đúng trang.
    /// Không thấy (đã xuất/hủy/không tồn tại) → 404 kèm thông báo.</summary>
    [HttpGet("find")]
    public async Task<ActionResult<FindLocationDto>> Find(string barcode)
    {
        var rows = (await _db.QueryAsync(
            @"SELECT l.LocationId, l.RackNo, l.LevelNo, l.Code,
                     (SELECT COUNT(*) FROM PMC_Materials m2
                       WHERE m2.CurrentLocationId = m.CurrentLocationId
                         AND m2.IsArchived = 0 AND m2.Status IN ('InStock','PartiallyIssued')
                         AND m2.Barcode <= m.Barcode) AS Ordinal
                FROM PMC_Materials m
                JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0 AND UPPER(m.Barcode) = UPPER(:bc)
                 AND m.Status IN ('InStock', 'PartiallyIssued')",
            new OracleParameter("bc", barcode))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = $"Không tìm thấy barcode '{barcode}' trong kho (có thể đã xuất/hủy)." });
        }

        var r = rows[0];
        return Ok(new FindLocationDto
        {
            LocationId = Convert.ToInt32(r["LOCATIONID"]),
            RackNo = Convert.ToInt32(r["RACKNO"]),
            LevelNo = Convert.ToInt32(r["LEVELNO"]),
            Code = r["CODE"]?.ToString() ?? string.Empty,
            Ordinal = Convert.ToInt32(r["ORDINAL"]),
        });
    }

    /// <summary>
    /// Dữ liệu cho màn hình giám sát TV: tổng quan toàn kho + 5 lượt lên kệ gần nhất + 5 lượt xuất
    /// kho gần nhất. Web gọi lại định kỳ / khi có realtime "warehouseChanged" để cập nhật.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<WarehouseDashboardDto>> Dashboard()
    {
        var summaryRows = (await _db.QueryAsync(
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

        var inboundRows = await _db.QueryAsync(
            @"SELECT * FROM (
                  SELECT mv.MaterialId, m.Barcode, m.Dev, m.Model, mv.Qty, m.Unit, l.Code AS LocationCode, mv.OccurredAt
                    FROM PMC_StockMovements mv
                    JOIN PMC_Materials m ON m.MaterialId = mv.MaterialId
                    LEFT JOIN PMC_StorageLocations l ON l.LocationId = mv.LocationId
                   WHERE mv.MovementType = 'Inbound'
                   ORDER BY mv.OccurredAt DESC
              ) WHERE ROWNUM <= 5");

        var issueRows = await _db.QueryAsync(
            @"SELECT * FROM (
                  SELECT mv.MaterialId, m.Barcode, m.Dev, m.Model, mv.Qty, m.Unit, r.Name AS RecipientName, mv.OccurredAt
                    FROM PMC_StockMovements mv
                    JOIN PMC_Materials m ON m.MaterialId = mv.MaterialId
                    LEFT JOIN PMC_Recipients r ON r.RecipientId = mv.RecipientId
                   WHERE mv.MovementType = 'IssueToWorkshop'
                   ORDER BY mv.OccurredAt DESC
              ) WHERE ROWNUM <= 5");

        return Ok(new WarehouseDashboardDto
        {
            Summary = summary,
            RecentInbound = inboundRows.Select(MapActivity).ToList(),
            RecentIssue = issueRows.Select(MapActivity).ToList(),
        });
    }

    /// <summary>
    /// Toàn bộ hoạt động kho hôm nay (nhập/xuất/hủy/nhận lại), mới nhất trước — dùng cho app mobile.
    /// Lọc thêm theo loại (Inbound/IssueToWorkshop/Dispose/Return) nếu truyền movementType.
    /// </summary>
    [HttpGet("today-log")]
    public async Task<ActionResult<PagedResult<TodayLogItem>>> TodayLog(string? movementType, int page = 1, int pageSize = 20)
    {
        const string innerSql =
            @"SELECT mv.MovementId, mv.MaterialId, m.Barcode, m.Dev, m.Model, mv.MovementType, mv.Qty, m.Unit,
                     l.Code AS LocationCode, u.Username, r.Name AS RecipientName, mv.Note, mv.OccurredAt
                FROM PMC_StockMovements mv
                JOIN PMC_Materials m ON m.MaterialId = mv.MaterialId
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = mv.LocationId
                LEFT JOIN PMC_Users u ON u.UserId = mv.UserId
                LEFT JOIN PMC_Recipients r ON r.RecipientId = mv.RecipientId
               WHERE TRUNC(mv.OccurredAt) = TRUNC(SYSDATE)
                 AND (:movementType IS NULL OR mv.MovementType = :movementType)
               ORDER BY mv.OccurredAt DESC, mv.MovementId DESC";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize,
            new OracleParameter("movementType", (object?)movementType ?? DBNull.Value));

        var items = paged.Items.Select(r => new TodayLogItem
        {
            MovementId = Convert.ToInt32(r["MOVEMENTID"]),
            MaterialId = Convert.ToInt32(r["MATERIALID"]),
            Barcode = r["BARCODE"]?.ToString() ?? string.Empty,
            Dev = r["DEV"]?.ToString(),
            Model = r["MODEL"]?.ToString(),
            MovementType = r["MOVEMENTTYPE"]?.ToString() ?? string.Empty,
            Qty = Convert.ToDecimal(r["QTY"]),
            Unit = r["UNIT"]?.ToString(),
            LocationCode = r["LOCATIONCODE"]?.ToString(),
            Username = r["USERNAME"]?.ToString(),
            RecipientName = r["RECIPIENTNAME"]?.ToString(),
            Note = r["NOTE"]?.ToString(),
            OccurredAt = Convert.ToDateTime(r["OCCURREDAT"]),
        }).ToList();

        return Ok(new PagedResult<TodayLogItem>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
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
