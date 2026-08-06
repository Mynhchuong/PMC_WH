using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialsController : ControllerBase
{
    private const int MaxBatchSize = 40;

    private const string InsertSql = @"
        INSERT INTO PMC_Materials
            (Barcode, CsCode, Dev, PoNo, Supplier, Model, Season, Stage, Colorway, Component,
             MatlDescription, ColorCode, ColorName, SizeSpec, ArrivalQty, Unit, FocFlag, ArrivalDate,
             Remark, Testing, TestRequire, TestQty, Category, RequestBy, RequestOn)
        VALUES
            (:Barcode, :CsCode, :Dev, :PoNo, :Supplier, :Model, :Season, :Stage, :Colorway, :Component,
             :MatlDescription, :ColorCode, :ColorName, :SizeSpec, :ArrivalQty, :Unit, :FocFlag, :ArrivalDate,
             :Remark, :Testing, :TestRequire, :TestQty, :Category, :RequestBy, :RequestOn)";

    private readonly OracleDataService _db;

    public MaterialsController(OracleDataService db)
    {
        _db = db;
    }

    /// <summary>
    /// Danh sách liệu (phân trang), lọc theo Barcode, Status, và khoảng Ngày Nhập (ArrivalDate).
    /// Luôn ẩn IsArchived=1 (đã xoá mềm).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MaterialListItem>>> Get(
        string? barcode, string? status, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 20)
    {
        const string innerSql = @"
            SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec,
                   m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.ArrivalDate, m.CreatedAt
              FROM PMC_Materials m
              LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
             WHERE m.IsArchived = 0
               AND (:barcode IS NULL OR UPPER(m.Barcode) LIKE '%' || UPPER(:barcode) || '%')
               AND (:status IS NULL OR m.Status = :status)
               AND (:fromDate IS NULL OR m.ArrivalDate >= :fromDate)
               AND (:toDate IS NULL OR m.ArrivalDate < :toDate + 1)
             ORDER BY m.CreatedAt DESC";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize,
            new OracleParameter("barcode", (object?)barcode ?? DBNull.Value),
            new OracleParameter("status", (object?)status ?? DBNull.Value),
            new OracleParameter("fromDate", OracleDbType.Date) { Value = (object?)fromDate ?? DBNull.Value },
            new OracleParameter("toDate", OracleDbType.Date) { Value = (object?)toDate ?? DBNull.Value });

        var items = paged.Items.Select(MapMaterialListItem).ToList();

        return Ok(new PagedResult<MaterialListItem>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    /// <summary>
    /// Chi tiết đầy đủ 1 liệu — dùng cho popup xem detail.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaterialDetail>> GetById(int id)
    {
        var rows = (await _db.QueryAsync(
            @"SELECT MaterialId, Barcode, CsCode, Dev, PoNo, Supplier, Model, Season, Stage, Colorway,
                     Component, MatlDescription, ColorCode, ColorName, SizeSpec, ArrivalQty, Unit, FocFlag,
                     ArrivalDate, Remark, Testing, TestRequire, TestQty, Category, RequestBy, RequestOn,
                     Balance, Status, StockedInAt, LastIssuedAt, DisposedAt, IsOverdue, CreatedAt, UpdatedAt
                FROM PMC_Materials
               WHERE MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound();
        }

        return Ok(MapMaterialDetail(rows[0]));
    }

    /// <summary>
    /// Tra 1 liệu theo đúng barcode (khớp tuyệt đối) — dùng cho màn quét (Inbound/Issue) tra cứu nhanh.
    /// </summary>
    [HttpGet("by-barcode/{barcode}")]
    public async Task<ActionResult<MaterialListItem>> GetByBarcode(string barcode)
    {
        var rows = (await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.ArrivalDate, m.CreatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0 AND UPPER(m.Barcode) = UPPER(:barcode)",
            new OracleParameter("barcode", barcode))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = $"Không tìm thấy barcode '{barcode}'." });
        }

        return Ok(MapMaterialListItem(rows[0]));
    }

    /// <summary>
    /// Quét lên kệ: chuyển 1 liệu từ Staging → InStock, gán ô kệ, ghi StockMovements (Inbound).
    /// Update Materials + insert StockMovements chạy trong CÙNG 1 transaction.
    /// </summary>
    [HttpPost("{id:int}/inbound")]
    public async Task<IActionResult> Inbound(int id, [FromBody] InboundRequest req)
    {
        var rows = (await _db.QueryAsync(
            "SELECT ArrivalQty, Status FROM PMC_Materials WHERE MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        var status = rows[0]["STATUS"]?.ToString();
        if (status != "Staging")
        {
            return Conflict(new { message = "Liệu này không còn ở trạng thái chờ (Staging) — có thể đã được người khác lên kệ." });
        }

        var arrivalQty = Convert.ToDecimal(rows[0]["ARRIVALQTY"]);

        var statements = new (string Sql, OracleParameter[] Parameters)[]
        {
            ("UPDATE PMC_Materials " +
             "   SET Status = 'InStock', CurrentLocationId = :LocationId, Balance = :Qty, " +
             "       StockedInAt = SYSTIMESTAMP, UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1 " +
             " WHERE MaterialId = :MaterialId AND Status = 'Staging'",
             new[]
             {
                 new OracleParameter("LocationId", req.LocationId),
                 new OracleParameter("Qty", arrivalQty),
                 new OracleParameter("UserId", req.UserId),
                 new OracleParameter("MaterialId", id),
             }),
            ("INSERT INTO PMC_StockMovements (MaterialId, MovementType, Qty, LocationId, UserId) " +
             "VALUES (:MaterialId, 'Inbound', :Qty, :LocationId, :UserId)",
             new[]
             {
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("Qty", arrivalQty),
                 new OracleParameter("LocationId", req.LocationId),
                 new OracleParameter("UserId", req.UserId),
             }),
        };

        await _db.ExecuteBatchAsync(statements);

        return Ok();
    }

    /// <summary>
    /// Danh sách liệu có thể xuất (InStock hoặc PartiallyIssued, còn Balance > 0).
    /// </summary>
    [HttpGet("issuable")]
    public async Task<ActionResult<List<MaterialListItem>>> Issuable()
    {
        var rows = await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.ArrivalDate, m.CreatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0
                 AND m.Status IN ('InStock', 'PartiallyIssued')
                 AND m.Balance > 0
               ORDER BY m.LastIssuedAt NULLS FIRST, m.CreatedAt");

        return Ok(rows.Select(MapMaterialListItem).ToList());
    }

    /// <summary>
    /// Xuất cho DEV/Workshop: trừ Balance, chuyển Status sang PartiallyIssued (còn dư)
    /// hoặc IssuedOut (hết), ghi StockMovements (IssueToWorkshop).
    /// Update Materials + insert StockMovements chạy trong CÙNG 1 transaction.
    /// </summary>
    [HttpPost("{id:int}/issue")]
    public async Task<IActionResult> Issue(int id, [FromBody] IssueRequest req)
    {
        if (req.Qty <= 0)
        {
            return BadRequest(new { message = "Số lượng xuất phải lớn hơn 0." });
        }

        var rows = (await _db.QueryAsync(
            "SELECT Balance, Status FROM PMC_Materials WHERE MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        var status = rows[0]["STATUS"]?.ToString();
        if (status != "InStock" && status != "PartiallyIssued")
        {
            return Conflict(new { message = "Liệu này không ở trạng thái có thể xuất (phải đang InStock hoặc PartiallyIssued)." });
        }

        var balance = Convert.ToDecimal(rows[0]["BALANCE"]);
        if (req.Qty > balance)
        {
            return BadRequest(new { message = $"Số lượng xuất ({req.Qty}) vượt quá tồn hiện tại ({balance})." });
        }

        var remaining = balance - req.Qty;
        var newStatus = remaining <= 0 ? "IssuedOut" : "PartiallyIssued";

        var statements = new (string Sql, OracleParameter[] Parameters)[]
        {
            ("UPDATE PMC_Materials " +
             "   SET Balance = :Remaining, Status = :NewStatus, LastIssuedAt = SYSTIMESTAMP, " +
             "       UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1 " +
             " WHERE MaterialId = :MaterialId AND Status IN ('InStock', 'PartiallyIssued')",
             new[]
             {
                 new OracleParameter("Remaining", remaining),
                 new OracleParameter("NewStatus", newStatus),
                 new OracleParameter("UserId", req.UserId),
                 new OracleParameter("MaterialId", id),
             }),
            ("INSERT INTO PMC_StockMovements (MaterialId, MovementType, Qty, UserId, RecipientId) " +
             "VALUES (:MaterialId, 'IssueToWorkshop', :Qty, :UserId, :RecipientId)",
             new[]
             {
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("Qty", req.Qty),
                 new OracleParameter("UserId", req.UserId),
                 new OracleParameter("RecipientId", req.RecipientId),
             }),
        };

        await _db.ExecuteBatchAsync(statements);

        return Ok();
    }

    /// <summary>
    /// Import 1 batch (tối đa 40 dòng) vào PMC_Materials với Status mặc định 'Staging'.
    /// Bỏ qua (không insert) các dòng: barcode trống, ArrivalQty trống/không hợp lệ,
    /// trùng barcode trong chính batch, hoặc barcode đã có sẵn trong CSDL.
    /// </summary>
    [HttpPost("import-batch")]
    public async Task<ActionResult<MaterialImportBatchResult>> ImportBatch([FromBody] List<MaterialImportRow> rows)
    {
        if (rows == null || rows.Count == 0)
        {
            return BadRequest("Không có dòng nào để import.");
        }
        if (rows.Count > MaxBatchSize)
        {
            return BadRequest($"Mỗi batch tối đa {MaxBatchSize} dòng.");
        }

        var result = new MaterialImportBatchResult();
        var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<MaterialImportRow>();

        foreach (var row in rows)
        {
            var barcode = row.Barcode?.Trim();
            if (string.IsNullOrEmpty(barcode))
            {
                result.Skipped.Add(new MaterialImportSkip(row.Barcode ?? "(trống)", "Barcode trống"));
                continue;
            }
            if (row.ArrivalQty is null)
            {
                result.Skipped.Add(new MaterialImportSkip(barcode, "A.Q'TY (ArrivalQty) trống/không hợp lệ"));
                continue;
            }
            if (!seenBarcodes.Add(barcode))
            {
                result.Skipped.Add(new MaterialImportSkip(barcode, "Trùng barcode trong file import"));
                continue;
            }

            row.Barcode = barcode;
            candidates.Add(row);
        }

        if (candidates.Count > 0)
        {
            var existingBarcodes = await GetExistingBarcodesAsync(candidates.Select(r => r.Barcode!).ToList());

            var toInsert = new List<MaterialImportRow>();
            foreach (var row in candidates)
            {
                if (existingBarcodes.Contains(row.Barcode!))
                {
                    result.Skipped.Add(new MaterialImportSkip(row.Barcode!, "Barcode đã tồn tại trong CSDL"));
                }
                else
                {
                    toInsert.Add(row);
                }
            }

            if (toInsert.Count > 0)
            {
                var statements = toInsert.Select(r => (InsertSql, BuildInsertParams(r)));
                result.InsertedCount = await _db.ExecuteBatchAsync(statements);
            }
        }

        return Ok(result);
    }

    private async Task<HashSet<string>> GetExistingBarcodesAsync(List<string> barcodes)
    {
        var inClause = string.Join(",", barcodes.Select((_, i) => $":b{i}"));
        var parameters = barcodes.Select((b, i) => new OracleParameter($"b{i}", b)).ToArray();

        var rows = await _db.QueryAsync($"SELECT Barcode FROM PMC_Materials WHERE Barcode IN ({inClause})", parameters);
        return rows
            .Select(r => r["BARCODE"]?.ToString())
            .Where(b => b != null)
            .Select(b => b!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static MaterialListItem MapMaterialListItem(Dictionary<string, object?> row) => new()
    {
        MaterialId = Convert.ToInt32(row["MATERIALID"]),
        Barcode = row["BARCODE"]?.ToString() ?? string.Empty,
        Dev = row["DEV"]?.ToString(),
        PoNo = row["PONO"]?.ToString(),
        Supplier = row["SUPPLIER"]?.ToString(),
        Model = row["MODEL"]?.ToString(),
        Colorway = row["COLORWAY"]?.ToString(),
        SizeSpec = row["SIZESPEC"]?.ToString(),
        ArrivalQty = row["ARRIVALQTY"] != null ? Convert.ToDecimal(row["ARRIVALQTY"]) : null,
        Balance = row["BALANCE"] != null ? Convert.ToDecimal(row["BALANCE"]) : null,
        Unit = row["UNIT"]?.ToString(),
        Status = row["STATUS"]?.ToString() ?? string.Empty,
        LocationCode = row["LOCATIONCODE"]?.ToString(),
        ArrivalDate = row["ARRIVALDATE"] != null ? Convert.ToDateTime(row["ARRIVALDATE"]) : null,
        CreatedAt = Convert.ToDateTime(row["CREATEDAT"]),
    };

    private static MaterialDetail MapMaterialDetail(Dictionary<string, object?> row) => new()
    {
        MaterialId = Convert.ToInt32(row["MATERIALID"]),
        Barcode = row["BARCODE"]?.ToString() ?? string.Empty,
        CsCode = row["CSCODE"] != null ? Convert.ToInt32(row["CSCODE"]) : null,
        Dev = row["DEV"]?.ToString(),
        PoNo = row["PONO"]?.ToString(),
        Supplier = row["SUPPLIER"]?.ToString(),
        Model = row["MODEL"]?.ToString(),
        Season = row["SEASON"]?.ToString(),
        Stage = row["STAGE"]?.ToString(),
        Colorway = row["COLORWAY"]?.ToString(),
        Component = row["COMPONENT"]?.ToString(),
        MatlDescription = row["MATLDESCRIPTION"]?.ToString(),
        ColorCode = row["COLORCODE"]?.ToString(),
        ColorName = row["COLORNAME"]?.ToString(),
        SizeSpec = row["SIZESPEC"]?.ToString(),
        ArrivalQty = row["ARRIVALQTY"] != null ? Convert.ToDecimal(row["ARRIVALQTY"]) : null,
        Unit = row["UNIT"]?.ToString(),
        FocFlag = row["FOCFLAG"]?.ToString(),
        ArrivalDate = row["ARRIVALDATE"] != null ? Convert.ToDateTime(row["ARRIVALDATE"]) : null,
        Remark = row["REMARK"]?.ToString(),
        Testing = row["TESTING"] != null ? Convert.ToInt32(row["TESTING"]) : null,
        TestRequire = row["TESTREQUIRE"]?.ToString(),
        TestQty = row["TESTQTY"]?.ToString(),
        Category = row["CATEGORY"]?.ToString(),
        RequestBy = row["REQUESTBY"]?.ToString(),
        RequestOn = row["REQUESTON"] != null ? Convert.ToDateTime(row["REQUESTON"]) : null,
        Balance = row["BALANCE"] != null ? Convert.ToDecimal(row["BALANCE"]) : null,
        Status = row["STATUS"]?.ToString() ?? string.Empty,
        StockedInAt = row["STOCKEDINAT"] != null ? Convert.ToDateTime(row["STOCKEDINAT"]) : null,
        LastIssuedAt = row["LASTISSUEDAT"] != null ? Convert.ToDateTime(row["LASTISSUEDAT"]) : null,
        DisposedAt = row["DISPOSEDAT"] != null ? Convert.ToDateTime(row["DISPOSEDAT"]) : null,
        IsOverdue = row["ISOVERDUE"] != null && Convert.ToInt32(row["ISOVERDUE"]) == 1,
        CreatedAt = Convert.ToDateTime(row["CREATEDAT"]),
        UpdatedAt = row["UPDATEDAT"] != null ? Convert.ToDateTime(row["UPDATEDAT"]) : null,
    };

    private static OracleParameter[] BuildInsertParams(MaterialImportRow r) => new[]
    {
        new OracleParameter("Barcode", r.Barcode),
        new OracleParameter("CsCode", (object?)r.CsCode ?? DBNull.Value),
        new OracleParameter("Dev", (object?)r.Dev ?? DBNull.Value),
        new OracleParameter("PoNo", (object?)r.PoNo ?? DBNull.Value),
        new OracleParameter("Supplier", (object?)r.Supplier ?? DBNull.Value),
        new OracleParameter("Model", (object?)r.Model ?? DBNull.Value),
        new OracleParameter("Season", (object?)r.Season ?? DBNull.Value),
        new OracleParameter("Stage", (object?)r.Stage ?? DBNull.Value),
        new OracleParameter("Colorway", (object?)r.Colorway ?? DBNull.Value),
        new OracleParameter("Component", (object?)r.Component ?? DBNull.Value),
        new OracleParameter("MatlDescription", (object?)r.MatlDescription ?? DBNull.Value),
        new OracleParameter("ColorCode", (object?)r.ColorCode ?? DBNull.Value),
        new OracleParameter("ColorName", (object?)r.ColorName ?? DBNull.Value),
        new OracleParameter("SizeSpec", (object?)r.SizeSpec ?? DBNull.Value),
        new OracleParameter("ArrivalQty", r.ArrivalQty!.Value),
        new OracleParameter("Unit", (object?)r.Unit ?? DBNull.Value),
        new OracleParameter("FocFlag", (object?)r.FocFlag ?? DBNull.Value),
        new OracleParameter("ArrivalDate", (object?)r.ArrivalDate ?? DBNull.Value),
        new OracleParameter("Remark", (object?)r.Remark ?? DBNull.Value),
        new OracleParameter("Testing", (object?)r.Testing ?? DBNull.Value),
        new OracleParameter("TestRequire", (object?)r.TestRequire ?? DBNull.Value),
        new OracleParameter("TestQty", (object?)r.TestQty ?? DBNull.Value),
        new OracleParameter("Category", (object?)r.Category ?? DBNull.Value),
        new OracleParameter("RequestBy", (object?)r.RequestBy ?? DBNull.Value),
        new OracleParameter("RequestOn", (object?)r.RequestOn ?? DBNull.Value),
    };
}
