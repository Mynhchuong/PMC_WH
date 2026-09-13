using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StorageLocationsController : ControllerBase
{
    private readonly OracleDataService _db;

    public StorageLocationsController(OracleDataService db)
    {
        _db = db;
    }

    /// <summary>
    /// Danh sách ô kệ. Mặc định chỉ ô đang hoạt động (IsActive=1) — dùng cho combobox lúc quét lên
    /// kệ + bản đồ 3D. Truyền includeInactive=true để trang quản lý xem cả ô đã ẩn.
    /// Trả về list phẳng (không phân trang) — app mobile phụ thuộc shape này; Web tự phân trang.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StorageLocationDto>>> Get(bool includeInactive = false)
    {
        var sql =
            @"SELECT LocationId, RackNo, LevelNo, Code, IsActive, ManagerName, Purpose_Vi, Purpose_En, Note
                FROM PMC_StorageLocations "
            + (includeInactive ? "" : "WHERE IsActive = 1 ")
            + "ORDER BY RackNo, LevelNo";

        var rows = await _db.QueryAsync(sql);
        return Ok(rows.Select(MapLocation).ToList());
    }

    /// <summary>Tạo mới 1 ô kệ (số kệ + số tầng). Trigger TRG_PMC_LOC_BIU tự sinh LocationId + Code.</summary>
    [HttpPost]
    public async Task<ActionResult<StorageLocationDto>> Create([FromBody] CreateStorageLocationRequest req)
    {
        if (req.RackNo <= 0 || req.LevelNo <= 0)
        {
            return BadRequest(new { message = "Số kệ và số tầng phải là số nguyên dương." });
        }

        try
        {
            await _db.ExecuteAsync(
                @"INSERT INTO PMC_StorageLocations (RackNo, LevelNo, IsActive, ManagerName, Purpose_Vi, Purpose_En, Note)
                  VALUES (:RackNo, :LevelNo, 1, :ManagerName, :PurposeVi, :PurposeEn, :Note)",
                new OracleParameter("RackNo", req.RackNo),
                new OracleParameter("LevelNo", req.LevelNo),
                new OracleParameter("ManagerName", Trim(req.ManagerName)),
                new OracleParameter("PurposeVi", Trim(req.PurposeVi)),
                new OracleParameter("PurposeEn", Trim(req.PurposeEn)),
                new OracleParameter("Note", Trim(req.Note)));
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return Conflict(new { message = $"Kệ {req.RackNo}.{req.LevelNo} đã tồn tại." });
        }
        catch (OracleException ex) when (ex.Number == 12899)
        {
            // ORA-12899 (value too large for column) — người quản lý/công dụng/ghi chú nhập quá dài
            // (giới hạn cột: ManagerName 120, Purpose_Vi/Purpose_En/Note 400 ký tự).
            return BadRequest(new { message = "Người quản lý/công dụng/ghi chú quá dài, vui lòng rút ngắn lại." });
        }

        var rows = await _db.QueryAsync(
            @"SELECT LocationId, RackNo, LevelNo, Code, IsActive, ManagerName, Purpose_Vi, Purpose_En, Note
                FROM PMC_StorageLocations WHERE RackNo = :RackNo AND LevelNo = :LevelNo",
            new OracleParameter("RackNo", req.RackNo),
            new OracleParameter("LevelNo", req.LevelNo));

        return Ok(MapLocation(rows.First()));
    }

    /// <summary>Sửa thông tin mô tả của ô kệ (người quản lý, công dụng, ghi chú). Không đổi số kệ/tầng.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStorageLocationRequest req)
    {
        int affected;
        try
        {
            affected = await _db.ExecuteAsync(
                @"UPDATE PMC_StorageLocations
                     SET ManagerName = :ManagerName,
                         Purpose_Vi  = :PurposeVi,
                         Purpose_En  = :PurposeEn,
                         Note        = :Note
                   WHERE LocationId = :Id",
                new OracleParameter("ManagerName", Trim(req.ManagerName)),
                new OracleParameter("PurposeVi", Trim(req.PurposeVi)),
                new OracleParameter("PurposeEn", Trim(req.PurposeEn)),
                new OracleParameter("Note", Trim(req.Note)),
                new OracleParameter("Id", id));
        }
        catch (OracleException ex) when (ex.Number == 12899)
        {
            return BadRequest(new { message = "Người quản lý/công dụng/ghi chú quá dài, vui lòng rút ngắn lại." });
        }

        return affected == 0 ? NotFound() : Ok();
    }

    /// <summary>
    /// Bật/tắt 1 ô kệ. Khi tắt (đang active) mà còn liệu đang nằm trên kệ đó thì chặn — phải
    /// chuyển/xuất hết liệu trước.
    /// </summary>
    [HttpPost("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var rows = (await _db.QueryAsync(
            "SELECT IsActive FROM PMC_StorageLocations WHERE LocationId = :Id",
            new OracleParameter("Id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound();
        }

        var currentlyActive = Convert.ToInt32(rows[0]["ISACTIVE"]) == 1;

        if (currentlyActive)
        {
            var onShelf = Convert.ToInt32((await _db.QueryAsync(
                @"SELECT COUNT(*) AS c FROM PMC_Materials
                   WHERE CurrentLocationId = :Id AND IsArchived = 0
                     AND Status IN ('InStock', 'PartiallyIssued')",
                new OracleParameter("Id", id))).First()["C"]);

            if (onShelf > 0)
            {
                return Conflict(new { message = $"Không thể ẩn kệ này — còn {onShelf} liệu đang nằm trên kệ. Hãy chuyển/xuất hết trước." });
            }
        }

        await _db.ExecuteAsync(
            "UPDATE PMC_StorageLocations SET IsActive = 1 - IsActive WHERE LocationId = :Id",
            new OracleParameter("Id", id));

        return Ok();
    }

    /// <summary>
    /// Import hàng loạt từ Excel (PMC xuất ra, sửa, import lại) — khớp theo (RackNo, LevelNo):
    /// đã có ô kệ đó thì UPDATE thông tin mô tả, chưa có thì INSERT mới (trigger tự sinh LocationId +
    /// Code). Mỗi dòng xử lý độc lập — 1 dòng lỗi (VD text quá dài) không làm rớt cả lô, giống
    /// nguyên tắc ở api/Materials/import-batch. Không đổi IsActive qua đường này. Việc kiểm tra
    /// trùng (RackNo, LevelNo) NGAY TRONG FILE nằm ở Web (PmcWh.Web StorageLocationsController.Index
    /// (POST)) — tới đây thì coi mỗi dòng độc lập.
    /// </summary>
    [HttpPost("import-batch")]
    public async Task<ActionResult<StorageLocationImportBatchResult>> ImportBatch([FromBody] List<StorageLocationImportRow> rows)
    {
        var result = new StorageLocationImportBatchResult();

        foreach (var row in rows)
        {
            var code = $"{row.RackNo}.{row.LevelNo}";
            if (row.RackNo <= 0 || row.LevelNo <= 0)
            {
                result.Skipped.Add(new StorageLocationImportSkip(code, "Số kệ/tầng không hợp lệ."));
                continue;
            }

            try
            {
                var existing = (await _db.QueryAsync(
                    "SELECT LocationId FROM PMC_StorageLocations WHERE RackNo = :RackNo AND LevelNo = :LevelNo",
                    new OracleParameter("RackNo", row.RackNo),
                    new OracleParameter("LevelNo", row.LevelNo))).ToList();

                if (existing.Count > 0)
                {
                    var locationId = Convert.ToInt32(existing[0]["LOCATIONID"]);
                    await _db.ExecuteAsync(
                        @"UPDATE PMC_StorageLocations
                             SET ManagerName = :ManagerName, Purpose_Vi = :PurposeVi, Purpose_En = :PurposeEn, Note = :Note
                           WHERE LocationId = :Id",
                        new OracleParameter("ManagerName", Trim(row.ManagerName)),
                        new OracleParameter("PurposeVi", Trim(row.PurposeVi)),
                        new OracleParameter("PurposeEn", Trim(row.PurposeEn)),
                        new OracleParameter("Note", Trim(row.Note)),
                        new OracleParameter("Id", locationId));
                    result.UpdatedCount++;
                }
                else
                {
                    await _db.ExecuteAsync(
                        @"INSERT INTO PMC_StorageLocations (RackNo, LevelNo, IsActive, ManagerName, Purpose_Vi, Purpose_En, Note)
                          VALUES (:RackNo, :LevelNo, 1, :ManagerName, :PurposeVi, :PurposeEn, :Note)",
                        new OracleParameter("RackNo", row.RackNo),
                        new OracleParameter("LevelNo", row.LevelNo),
                        new OracleParameter("ManagerName", Trim(row.ManagerName)),
                        new OracleParameter("PurposeVi", Trim(row.PurposeVi)),
                        new OracleParameter("PurposeEn", Trim(row.PurposeEn)),
                        new OracleParameter("Note", Trim(row.Note)));
                    result.InsertedCount++;
                }
            }
            catch (OracleException ex) when (ex.Number == 12899)
            {
                result.Skipped.Add(new StorageLocationImportSkip(code, "Người quản lý/công dụng/ghi chú quá dài (tối đa 120/400 ký tự)."));
            }
            catch (OracleException ex)
            {
                result.Skipped.Add(new StorageLocationImportSkip(code, $"Lỗi cơ sở dữ liệu: {ex.Message}"));
            }
        }

        return Ok(result);
    }

    private static object Trim(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

    private static StorageLocationDto MapLocation(Dictionary<string, object?> row) => new()
    {
        LocationId = Convert.ToInt32(row["LOCATIONID"]),
        RackNo = Convert.ToInt32(row["RACKNO"]),
        LevelNo = Convert.ToInt32(row["LEVELNO"]),
        Code = row["CODE"]?.ToString() ?? string.Empty,
        IsActive = Convert.ToInt32(row["ISACTIVE"]) == 1,
        ManagerName = row["MANAGERNAME"]?.ToString(),
        PurposeVi = row["PURPOSE_VI"]?.ToString(),
        PurposeEn = row["PURPOSE_EN"]?.ToString(),
        Note = row["NOTE"]?.ToString(),
    };
}
