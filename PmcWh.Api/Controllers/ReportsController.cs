using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly OracleDataService _db;

    public ReportsController(OracleDataService db)
    {
        _db = db;
    }

    // DaysOut chỉ tính cho lần xuất GẦN NHẤT của liệu (RN = 1, xác định bằng ROW_NUMBER theo MaterialId —
    // KHÔNG so khớp LastIssuedAt = OccurredAt vì đó là 2 lần gọi SYSTIMESTAMP độc lập ở 2 câu lệnh khác
    // nhau, lệch nhau vài mili-giây nên gần như không bao giờ khớp tuyệt đối) và khi liệu vẫn chưa nhận
    // lại hết (Status IssuedOut/PartiallyIssued). RN được tính trên TOÀN BỘ lịch sử xuất (trước khi áp
    // filter ngày/nơi nhận), để lọc theo ngày không làm sai lệch việc xác định "lần xuất gần nhất".
    private const string BaseIssueSql = @"
        SELECT MovementId, MaterialId, Barcode, Dev, Model, Colorway, SizeSpec, Unit, Qty, OccurredAt,
               RecipientName, Username, Status, IsOverdue,
               CASE WHEN RN = 1 AND Status IN ('IssuedOut', 'PartiallyIssued')
                    THEN TRUNC(SYSDATE) - TRUNC(OccurredAt)
                    ELSE NULL END AS DaysOut
          FROM (
                SELECT mv.MovementId, m.MaterialId, m.Barcode, m.Dev, m.Model, m.Colorway, m.SizeSpec, m.Unit,
                       mv.Qty, mv.OccurredAt, mv.RecipientId, r.Name AS RecipientName, u.Username,
                       m.Status, m.IsOverdue,
                       ROW_NUMBER() OVER (PARTITION BY mv.MaterialId ORDER BY mv.OccurredAt DESC) AS RN
                  FROM PMC_StockMovements mv
                  JOIN PMC_Materials m ON m.MaterialId = mv.MaterialId
                  LEFT JOIN PMC_Recipients r ON r.RecipientId = mv.RecipientId
                  LEFT JOIN PMC_Users u ON u.UserId = mv.UserId
                 WHERE mv.MovementType = 'IssueToWorkshop'
               ) base
         WHERE (:fromDate IS NULL OR OccurredAt >= :fromDate)
           AND (:toDate IS NULL OR OccurredAt < :toDate + 1)
           AND (:recipientId IS NULL OR RecipientId = :recipientId)";

    /// <summary>
    /// Báo cáo xuất kho (phân trang): lọc theo khoảng ngày xuất, nơi nhận, và tùy chọn chỉ hiện quá 90 ngày.
    /// </summary>
    [HttpGet("issues")]
    public async Task<ActionResult<PagedResult<IssueReportItem>>> Issues(
        DateTime? fromDate, DateTime? toDate, int? recipientId, bool? overdueOnly, int page = 1, int pageSize = 20)
    {
        var overdueClause = overdueOnly == true ? "WHERE DaysOut > 90" : "";
        var innerSql = $"SELECT * FROM ({BaseIssueSql}) t {overdueClause} ORDER BY OccurredAt DESC";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize, BuildParams(fromDate, toDate, recipientId));

        return Ok(new PagedResult<IssueReportItem>
        {
            Items = paged.Items.Select(MapItem).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    /// <summary>
    /// Xuất Excel: cùng bộ lọc như trên nhưng KHÔNG phân trang (giới hạn an toàn 5000 dòng).
    /// </summary>
    [HttpGet("issues/export")]
    public async Task<ActionResult<List<IssueReportItem>>> IssuesExport(
        DateTime? fromDate, DateTime? toDate, int? recipientId, bool? overdueOnly)
    {
        var overdueClause = overdueOnly == true ? "WHERE DaysOut > 90" : "";
        var sql = $@"SELECT * FROM (
                        SELECT * FROM ({BaseIssueSql}) t {overdueClause} ORDER BY OccurredAt DESC
                     ) WHERE ROWNUM <= 5000";

        var rows = await _db.QueryAsync(sql, BuildParams(fromDate, toDate, recipientId));

        return Ok(rows.Select(MapItem).ToList());
    }

    private static OracleParameter[] BuildParams(DateTime? fromDate, DateTime? toDate, int? recipientId) => new[]
    {
        new OracleParameter("fromDate", OracleDbType.Date) { Value = (object?)fromDate ?? DBNull.Value },
        new OracleParameter("toDate", OracleDbType.Date) { Value = (object?)toDate ?? DBNull.Value },
        new OracleParameter("recipientId", (object?)recipientId ?? DBNull.Value),
    };

    private static IssueReportItem MapItem(Dictionary<string, object?> row) => new()
    {
        MaterialId = Convert.ToInt32(row["MATERIALID"]),
        Barcode = row["BARCODE"]?.ToString() ?? string.Empty,
        Dev = row["DEV"]?.ToString(),
        Model = row["MODEL"]?.ToString(),
        Colorway = row["COLORWAY"]?.ToString(),
        SizeSpec = row["SIZESPEC"]?.ToString(),
        Unit = row["UNIT"]?.ToString(),
        Qty = Convert.ToDecimal(row["QTY"]),
        OccurredAt = Convert.ToDateTime(row["OCCURREDAT"]),
        RecipientName = row["RECIPIENTNAME"]?.ToString(),
        Username = row["USERNAME"]?.ToString(),
        Status = row["STATUS"]?.ToString() ?? string.Empty,
        IsOverdue = row["ISOVERDUE"] != null && Convert.ToInt32(row["ISOVERDUE"]) == 1,
        DaysOut = row["DAYSOUT"] != null ? Convert.ToInt32(row["DAYSOUT"]) : null,
    };
}
