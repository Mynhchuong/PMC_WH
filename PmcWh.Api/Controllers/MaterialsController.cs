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
