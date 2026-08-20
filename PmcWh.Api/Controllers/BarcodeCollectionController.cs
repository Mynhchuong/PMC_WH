using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

/// <summary>
/// "Thu thập Barcode" — công cụ quét-đếm rời rạc với nghiệp vụ kho chính (không đụng
/// PMC_Materials/StockMovements). Bạn quản lý liệu test dùng để quét hàng loạt lấy danh sách mã +
/// số lần quét, đối chiếu vào file tracking riêng. Quét trên Android, xem/xoá/xuất Excel trên Web —
/// 2 bảng riêng PMC_BarcodeLists/PMC_BarcodeListItems, xoá thẳng (không soft-delete) vì đây là dữ
/// liệu tracking tạm, không phải chứng từ kho cần giữ lịch sử.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BarcodeCollectionController : ControllerBase
{
    private readonly OracleDataService _db;

    public BarcodeCollectionController(OracleDataService db)
    {
        _db = db;
    }

    [HttpGet("lists")]
    public async Task<ActionResult<List<BarcodeListDto>>> GetLists()
    {
        var rows = await _db.QueryAsync(
            @"SELECT l.ListId, l.Name, l.CreatedAt,
                     COUNT(i.ItemId) AS ItemCount,
                     NVL(SUM(i.ScanCount), 0) AS TotalScans
                FROM PMC_BarcodeLists l
                LEFT JOIN PMC_BarcodeListItems i ON i.ListId = l.ListId
               GROUP BY l.ListId, l.Name, l.CreatedAt
               ORDER BY l.CreatedAt DESC");

        return Ok(rows.Select(MapList).ToList());
    }

    [HttpPost("lists")]
    public async Task<ActionResult<BarcodeListDto>> CreateList([FromBody] CreateBarcodeListRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest(new { message = "Tên list không được để trống." });
        }

        var seqRows = await _db.QueryAsync("SELECT PMC_BarcodeListSeq.NEXTVAL AS NEXTVAL FROM DUAL");
        var listId = Convert.ToInt32(seqRows.First()["NEXTVAL"]);

        await _db.ExecuteAsync(
            "INSERT INTO PMC_BarcodeLists (ListId, Name, CreatedBy) VALUES (:ListId, :Name, :CreatedBy)",
            new OracleParameter("ListId", listId),
            new OracleParameter("Name", req.Name.Trim()),
            new OracleParameter("CreatedBy", (object?)req.UserId ?? DBNull.Value));

        var rows = await _db.QueryAsync(
            "SELECT ListId, Name, CreatedAt, 0 AS ItemCount, 0 AS TotalScans FROM PMC_BarcodeLists WHERE ListId = :ListId",
            new OracleParameter("ListId", listId));

        return Ok(MapList(rows.First()));
    }

    [HttpDelete("lists/{id:int}")]
    public async Task<IActionResult> DeleteList(int id)
    {
        await _db.ExecuteAsync("DELETE FROM PMC_BarcodeListItems WHERE ListId = :Id", new OracleParameter("Id", id));
        var affected = await _db.ExecuteAsync("DELETE FROM PMC_BarcodeLists WHERE ListId = :Id", new OracleParameter("Id", id));

        if (affected == 0)
        {
            return NotFound(new { message = "Không tìm thấy list." });
        }

        return Ok();
    }

    [HttpGet("lists/{id:int}/items")]
    public async Task<ActionResult<List<BarcodeListItemDto>>> GetItems(int id)
    {
        var rows = await _db.QueryAsync(
            "SELECT ItemId, Barcode, ScanCount, LastScannedAt FROM PMC_BarcodeListItems WHERE ListId = :ListId ORDER BY LastScannedAt DESC",
            new OracleParameter("ListId", id));

        return Ok(rows.Select(MapItem).ToList());
    }

    /// <summary>Mã đã có trong list → tăng ScanCount; chưa có → thêm mới ScanCount=1. Không dùng
    /// MERGE (cú pháp mới hơn Oracle 10g) — kiểm tra tồn tại rồi UPDATE hoặc INSERT.</summary>
    [HttpPost("lists/{id:int}/scan")]
    public async Task<ActionResult<ScanBarcodeResult>> Scan(int id, [FromBody] ScanBarcodeRequest req)
    {
        var barcode = req.Barcode?.Trim() ?? string.Empty;
        if (barcode.Length == 0)
        {
            return BadRequest(new { message = "Barcode không được để trống." });
        }

        var listExists = await _db.QueryAsync("SELECT ListId FROM PMC_BarcodeLists WHERE ListId = :Id", new OracleParameter("Id", id));
        if (!listExists.Any())
        {
            return NotFound(new { message = "Không tìm thấy list." });
        }

        var existing = await _db.QueryAsync(
            "SELECT ItemId, ScanCount FROM PMC_BarcodeListItems WHERE ListId = :ListId AND Barcode = :Barcode",
            new OracleParameter("ListId", id), new OracleParameter("Barcode", barcode));

        if (existing.Any())
        {
            var newCount = Convert.ToInt32(existing.First()["SCANCOUNT"]) + 1;
            await _db.ExecuteAsync(
                "UPDATE PMC_BarcodeListItems SET ScanCount = :ScanCount, LastScannedAt = SYSTIMESTAMP WHERE ItemId = :ItemId",
                new OracleParameter("ScanCount", newCount),
                new OracleParameter("ItemId", Convert.ToInt32(existing.First()["ITEMID"])));

            return Ok(new ScanBarcodeResult { IsDuplicate = true, ScanCount = newCount });
        }

        var seqRows = await _db.QueryAsync("SELECT PMC_BarcodeItemSeq.NEXTVAL AS NEXTVAL FROM DUAL");
        var itemId = Convert.ToInt32(seqRows.First()["NEXTVAL"]);

        try
        {
            await _db.ExecuteAsync(
                "INSERT INTO PMC_BarcodeListItems (ItemId, ListId, Barcode, ScanCount) VALUES (:ItemId, :ListId, :Barcode, 1)",
                new OracleParameter("ItemId", itemId),
                new OracleParameter("ListId", id),
                new OracleParameter("Barcode", barcode));

            return Ok(new ScanBarcodeResult { IsDuplicate = false, ScanCount = 1 });
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            // 2 máy quét cùng 1 mã mới vào cùng list gần như đồng thời: cả 2 request đều SELECT
            // thấy "chưa có" rồi cùng INSERT — request tới sau đụng UNIQUE constraint (ORA-00001).
            // Thay vì trả lỗi 500 thô, coi như quét trùng và cộng dồn ScanCount như bình thường.
            var raceRows = await _db.QueryAsync(
                "SELECT ItemId, ScanCount FROM PMC_BarcodeListItems WHERE ListId = :ListId AND Barcode = :Barcode",
                new OracleParameter("ListId", id), new OracleParameter("Barcode", barcode));
            var newCount = Convert.ToInt32(raceRows.First()["SCANCOUNT"]) + 1;
            await _db.ExecuteAsync(
                "UPDATE PMC_BarcodeListItems SET ScanCount = :ScanCount, LastScannedAt = SYSTIMESTAMP WHERE ItemId = :ItemId",
                new OracleParameter("ScanCount", newCount),
                new OracleParameter("ItemId", Convert.ToInt32(raceRows.First()["ITEMID"])));

            return Ok(new ScanBarcodeResult { IsDuplicate = true, ScanCount = newCount });
        }
    }

    [HttpDelete("lists/{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> DeleteItem(int id, int itemId)
    {
        var affected = await _db.ExecuteAsync(
            "DELETE FROM PMC_BarcodeListItems WHERE ListId = :ListId AND ItemId = :ItemId",
            new OracleParameter("ListId", id), new OracleParameter("ItemId", itemId));

        if (affected == 0)
        {
            return NotFound(new { message = "Không tìm thấy mã này trong list." });
        }

        return Ok();
    }

    private static BarcodeListDto MapList(Dictionary<string, object?> row) => new()
    {
        ListId = Convert.ToInt32(row["LISTID"]),
        Name = row["NAME"]?.ToString() ?? string.Empty,
        CreatedAt = Convert.ToDateTime(row["CREATEDAT"]),
        ItemCount = Convert.ToInt32(row["ITEMCOUNT"]),
        TotalScans = Convert.ToInt32(row["TOTALSCANS"]),
    };

    private static BarcodeListItemDto MapItem(Dictionary<string, object?> row) => new()
    {
        ItemId = Convert.ToInt32(row["ITEMID"]),
        Barcode = row["BARCODE"]?.ToString() ?? string.Empty,
        ScanCount = Convert.ToInt32(row["SCANCOUNT"]),
        LastScannedAt = Convert.ToDateTime(row["LASTSCANNEDAT"]),
    };
}
