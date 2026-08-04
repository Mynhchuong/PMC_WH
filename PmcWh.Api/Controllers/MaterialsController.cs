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
            SELECT MaterialId, Barcode, Dev, PoNo, Supplier, Model, Colorway, SizeSpec,
                   ArrivalQty, Unit, Status, ArrivalDate, CreatedAt
              FROM PMC_Materials
             WHERE IsArchived = 0
               AND (:barcode IS NULL OR UPPER(Barcode) LIKE '%' || UPPER(:barcode) || '%')
               AND (:status IS NULL OR Status = :status)
               AND (:fromDate IS NULL OR ArrivalDate >= :fromDate)
               AND (:toDate IS NULL OR ArrivalDate < :toDate + 1)
             ORDER BY CreatedAt DESC";

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
        Unit = row["UNIT"]?.ToString(),
        Status = row["STATUS"]?.ToString() ?? string.Empty,
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
