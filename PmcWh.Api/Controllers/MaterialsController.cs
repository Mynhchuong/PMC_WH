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
             Remark, Testing, TestRequire, TestQty, Category, RequestOn, MatlType, Pic, Mat)
        VALUES
            (:Barcode, :CsCode, :Dev, :PoNo, :Supplier, :Model, :Season, :Stage, :Colorway, :Component,
             :MatlDescription, :ColorCode, :ColorName, :SizeSpec, :ArrivalQty, :Unit, :FocFlag, :ArrivalDate,
             :Remark, :Testing, :TestRequire, :TestQty, :Category, :RequestOn, :MatlType, :Pic, :Mat)";

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
        string? barcode, string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue, int page = 1, int pageSize = 20)
    {
        const string innerSql = @"
            SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                   m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
              FROM PMC_Materials m
              LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
             WHERE m.IsArchived = 0
               AND (:barcode IS NULL OR UPPER(m.Barcode) LIKE '%' || UPPER(:barcode) || '%')
               AND (:status IS NULL OR m.Status = :status)
               AND (:fromDate IS NULL OR m.ArrivalDate >= :fromDate)
               AND (:toDate IS NULL OR m.ArrivalDate < :toDate + 1)
               AND (:isOverdue IS NULL OR m.IsOverdue = :isOverdue)
             ORDER BY m.CreatedAt DESC";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize,
            new OracleParameter("barcode", (object?)barcode ?? DBNull.Value),
            new OracleParameter("status", (object?)status ?? DBNull.Value),
            new OracleParameter("fromDate", OracleDbType.Date) { Value = (object?)fromDate ?? DBNull.Value },
            new OracleParameter("toDate", OracleDbType.Date) { Value = (object?)toDate ?? DBNull.Value },
            new OracleParameter("isOverdue", (object?)(isOverdue.HasValue ? (isOverdue.Value ? 1 : 0) : null) ?? DBNull.Value));

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
    /// Xuất Excel: cùng bộ lọc như danh sách nhưng KHÔNG phân trang, trả đủ mọi field mô tả
    /// (giống hệt cột trong file import PMC) — giới hạn an toàn 5000 dòng.
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult<List<MaterialDetail>>> Export(
        string? barcode, string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue)
    {
        var sql = @"
            SELECT * FROM (
                SELECT m.MaterialId, m.Barcode, m.CsCode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Season, m.Stage,
                       m.Colorway, m.Component, m.MatlDescription, m.ColorCode, m.ColorName, m.SizeSpec,
                       m.ArrivalQty, m.Unit, m.FocFlag, m.ArrivalDate, m.Remark, m.Testing, m.TestRequire,
                       m.TestQty, m.Category, m.RequestOn, m.MatlType, m.Pic, m.Mat,
                       m.Balance, m.Status, l.Code AS LocationCode, m.StockedInAt, m.LastIssuedAt,
                       m.DisposedAt, m.IsOverdue, m.CreatedAt, m.UpdatedAt
                  FROM PMC_Materials m
                  LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
                 WHERE m.IsArchived = 0
                   AND (:barcode IS NULL OR UPPER(m.Barcode) LIKE '%' || UPPER(:barcode) || '%')
                   AND (:status IS NULL OR m.Status = :status)
                   AND (:fromDate IS NULL OR m.ArrivalDate >= :fromDate)
                   AND (:toDate IS NULL OR m.ArrivalDate < :toDate + 1)
                   AND (:isOverdue IS NULL OR m.IsOverdue = :isOverdue)
                 ORDER BY m.CreatedAt DESC
            ) WHERE ROWNUM <= 5000";

        var rows = await _db.QueryAsync(sql,
            new OracleParameter("barcode", (object?)barcode ?? DBNull.Value),
            new OracleParameter("status", (object?)status ?? DBNull.Value),
            new OracleParameter("fromDate", OracleDbType.Date) { Value = (object?)fromDate ?? DBNull.Value },
            new OracleParameter("toDate", OracleDbType.Date) { Value = (object?)toDate ?? DBNull.Value },
            new OracleParameter("isOverdue", (object?)(isOverdue.HasValue ? (isOverdue.Value ? 1 : 0) : null) ?? DBNull.Value));

        return Ok(rows.Select(MapMaterialDetail).ToList());
    }

    /// <summary>
    /// Chi tiết đầy đủ 1 liệu — dùng cho popup xem detail.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaterialDetail>> GetById(int id)
    {
        var rows = (await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.CsCode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Season, m.Stage, m.Colorway,
                     m.Component, m.MatlDescription, m.ColorCode, m.ColorName, m.SizeSpec, m.ArrivalQty, m.Unit, m.FocFlag,
                     m.ArrivalDate, m.Remark, m.Testing, m.TestRequire, m.TestQty, m.Category, m.RequestOn,
                     m.MatlType, m.Pic, m.Mat,
                     m.Balance, m.Status, l.Code AS LocationCode, m.StockedInAt, m.LastIssuedAt, m.DisposedAt,
                     m.IsOverdue, m.CreatedAt, m.UpdatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound();
        }

        var detail = MapMaterialDetail(rows[0]);

        var movementRows = await _db.QueryAsync(
            @"SELECT mv.MovementId, mv.MovementType, mv.Qty, mv.OccurredAt, mv.Note,
                     l.Code AS LocationCode, u.Username, r.Name AS RecipientName
                FROM PMC_StockMovements mv
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = mv.LocationId
                LEFT JOIN PMC_Users u ON u.UserId = mv.UserId
                LEFT JOIN PMC_Recipients r ON r.RecipientId = mv.RecipientId
               WHERE mv.MaterialId = :id
               ORDER BY mv.OccurredAt DESC, mv.MovementId DESC",
            new OracleParameter("id", id));

        detail.Movements = movementRows.Select(MapMovementHistoryItem).ToList();

        return Ok(detail);
    }

    /// <summary>
    /// Sửa thông tin mô tả (Dev, PO, Model, Colorway, v.v.) — KHÔNG sửa ArrivalQty/Balance/Status,
    /// các trường đó do nghiệp vụ nhập/xuất/hủy/nhận lại tự tính. Admin-only (chặn ở tầng Web).
    /// </summary>
    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, [FromBody] EditMaterialRequest req)
    {
        if (req.ArrivalQty is null || req.ArrivalQty <= 0)
        {
            return BadRequest(new { message = "Số lượng phải lớn hơn 0." });
        }

        var rows = (await _db.QueryAsync(
            "SELECT Status, ArrivalQty, Balance FROM PMC_Materials WHERE MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        var status = rows[0]["STATUS"]?.ToString();
        if (status == "Disposed")
        {
            return Conflict(new { message = "Liệu này đã bị hủy — không thể sửa thông tin." });
        }

        var oldArrivalQty = Convert.ToDecimal(rows[0]["ARRIVALQTY"]);
        var oldBalance = rows[0]["BALANCE"] != null ? Convert.ToDecimal(rows[0]["BALANCE"]) : (decimal?)null;
        var delta = req.ArrivalQty.Value - oldArrivalQty;
        var newBalance = oldBalance.HasValue ? oldBalance.Value + delta : (decimal?)null;

        if (oldBalance.HasValue && newBalance!.Value < 0)
        {
            return BadRequest(new
            {
                message = $"Không thể sửa số lượng xuống {req.ArrivalQty.Value} — liệu đã xuất {oldArrivalQty - oldBalance.Value}, số lượng mới phải >= số đã xuất.",
            });
        }

        var affected = await _db.ExecuteAsync(
            @"UPDATE PMC_Materials
                 SET ArrivalQty = :ArrivalQty, Balance = :Balance,
                     Dev = :Dev, PoNo = :PoNo, Supplier = :Supplier, Model = :Model, Season = :Season,
                     Stage = :Stage, Colorway = :Colorway, Component = :Component,
                     MatlDescription = :MatlDescription, ColorCode = :ColorCode, ColorName = :ColorName,
                     SizeSpec = :SizeSpec, Unit = :Unit, FocFlag = :FocFlag, ArrivalDate = :ArrivalDate,
                     Remark = :Remark, Testing = :Testing, TestRequire = :TestRequire, TestQty = :TestQty,
                     Category = :Category, RequestOn = :RequestOn,
                     MatlType = :MatlType, Pic = :Pic, Mat = :Mat,
                     UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1
               WHERE MaterialId = :MaterialId AND Status <> 'Disposed'",
            new OracleParameter("ArrivalQty", req.ArrivalQty.Value),
            new OracleParameter("Balance", (object?)newBalance ?? DBNull.Value),
            new OracleParameter("Dev", (object?)req.Dev ?? DBNull.Value),
            new OracleParameter("PoNo", (object?)req.PoNo ?? DBNull.Value),
            new OracleParameter("Supplier", (object?)req.Supplier ?? DBNull.Value),
            new OracleParameter("Model", (object?)req.Model ?? DBNull.Value),
            new OracleParameter("Season", (object?)req.Season ?? DBNull.Value),
            new OracleParameter("Stage", (object?)req.Stage ?? DBNull.Value),
            new OracleParameter("Colorway", (object?)req.Colorway ?? DBNull.Value),
            new OracleParameter("Component", (object?)req.Component ?? DBNull.Value),
            new OracleParameter("MatlDescription", (object?)req.MatlDescription ?? DBNull.Value),
            new OracleParameter("ColorCode", (object?)req.ColorCode ?? DBNull.Value),
            new OracleParameter("ColorName", (object?)req.ColorName ?? DBNull.Value),
            new OracleParameter("SizeSpec", (object?)req.SizeSpec ?? DBNull.Value),
            new OracleParameter("Unit", (object?)req.Unit ?? DBNull.Value),
            new OracleParameter("FocFlag", (object?)req.FocFlag ?? DBNull.Value),
            new OracleParameter("ArrivalDate", OracleDbType.Date) { Value = (object?)req.ArrivalDate ?? DBNull.Value },
            new OracleParameter("Remark", (object?)req.Remark ?? DBNull.Value),
            new OracleParameter("Testing", (object?)req.Testing ?? DBNull.Value),
            new OracleParameter("TestRequire", (object?)req.TestRequire ?? DBNull.Value),
            new OracleParameter("TestQty", (object?)req.TestQty ?? DBNull.Value),
            new OracleParameter("Category", (object?)req.Category ?? DBNull.Value),
            new OracleParameter("RequestOn", OracleDbType.Date) { Value = (object?)req.RequestOn ?? DBNull.Value },
            new OracleParameter("MatlType", (object?)req.MatlType ?? DBNull.Value),
            new OracleParameter("Pic", (object?)req.Pic ?? DBNull.Value),
            new OracleParameter("Mat", (object?)req.Mat ?? DBNull.Value),
            new OracleParameter("UserId", req.UserId),
            new OracleParameter("MaterialId", id));

        if (affected == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        return Ok();
    }

    /// <summary>
    /// Admin xóa (đánh cờ IsArchived) 1 liệu đang quá 90 ngày chưa nhận lại — xóa mềm, vẫn giữ
    /// lịch sử StockMovements. KHÔNG tự động chạy — chỉ Admin bấm tay (chặn ở tầng Web).
    /// </summary>
    [HttpPost("{id:int}/archive-overdue")]
    public async Task<IActionResult> ArchiveOverdue(int id, [FromBody] ArchiveOverdueRequest req)
    {
        var affected = await _db.ExecuteAsync(
            @"UPDATE PMC_Materials
                 SET IsArchived = 1, UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1
               WHERE MaterialId = :MaterialId AND IsOverdue = 1 AND IsArchived = 0",
            new OracleParameter("UserId", req.UserId),
            new OracleParameter("MaterialId", id));

        if (affected == 0)
        {
            return Conflict(new { message = "Liệu này không còn ở trạng thái quá hạn chưa xóa — có thể đã được nhận lại hoặc xóa trước đó." });
        }

        return Ok();
    }

    /// <summary>
    /// Tra 1 liệu theo đúng barcode (khớp tuyệt đối) — dùng cho màn quét (Inbound/Issue) tra cứu nhanh.
    /// </summary>
    [HttpGet("by-barcode/{barcode}")]
    public async Task<ActionResult<MaterialListItem>> GetByBarcode(string barcode)
    {
        var rows = (await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
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

        if (!await LocationExistsAsync(req.LocationId))
        {
            return BadRequest(new { message = "Vị trí kệ không hợp lệ hoặc không còn hoạt động." });
        }

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

        try
        {
            await _db.ExecuteBatchAsync(statements);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict(new { message = "Liệu này vừa được người khác lên kệ, vui lòng tải lại danh sách." });
        }

        return Ok();
    }

    /// <summary>
    /// Danh sách liệu có thể xuất (InStock hoặc PartiallyIssued, còn Balance > 0).
    /// </summary>
    [HttpGet("issuable")]
    public async Task<ActionResult<List<MaterialListItem>>> Issuable()
    {
        var rows = await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
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

        if (!await RecipientExistsAsync(req.RecipientId))
        {
            return BadRequest(new { message = "Nơi nhận không hợp lệ hoặc không còn hoạt động." });
        }

        var remaining = balance - req.Qty;
        var newStatus = remaining <= 0 ? "IssuedOut" : "PartiallyIssued";

        var statements = new (string Sql, OracleParameter[] Parameters)[]
        {
            ("UPDATE PMC_Materials " +
             "   SET Balance = :Remaining, Status = :NewStatus, LastIssuedAt = SYSTIMESTAMP, " +
             "       CurrentLocationId = CASE WHEN :NewStatus = 'IssuedOut' THEN NULL ELSE CurrentLocationId END, " +
             "       UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1 " +
             " WHERE MaterialId = :MaterialId AND Status IN ('InStock', 'PartiallyIssued') AND Balance = :OldBalance",
             new[]
             {
                 new OracleParameter("Remaining", remaining),
                 new OracleParameter("NewStatus", newStatus),
                 new OracleParameter("UserId", req.UserId),
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("OldBalance", balance),
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

        try
        {
            await _db.ExecuteBatchAsync(statements);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict(new { message = "Tồn kho của liệu này vừa bị thay đổi bởi thao tác khác, vui lòng tải lại và thử lại." });
        }

        return Ok();
    }

    /// <summary>
    /// Danh sách liệu đang out (IssuedOut/PartiallyIssued) — có thể nhận lại (Return).
    /// </summary>
    [HttpGet("returnable")]
    public async Task<ActionResult<List<MaterialListItem>>> Returnable()
    {
        var rows = await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0
                 AND m.Status IN ('IssuedOut', 'PartiallyIssued')
               ORDER BY m.LastIssuedAt NULLS FIRST, m.CreatedAt");

        return Ok(rows.Select(MapMaterialListItem).ToList());
    }

    /// <summary>
    /// Nhận lại hàng đã xuất: cộng lại Balance, về kệ (InStock nếu đủ, PartiallyIssued nếu còn thiếu),
    /// reset IsOverdue, ghi StockMovements (Return). Update Materials + insert Movement chạy CÙNG 1 transaction.
    /// </summary>
    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] ReturnRequest req)
    {
        if (req.Qty <= 0)
        {
            return BadRequest(new { message = "Số lượng nhận lại phải lớn hơn 0." });
        }

        var rows = (await _db.QueryAsync(
            "SELECT Balance, ArrivalQty, Status FROM PMC_Materials WHERE MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        var status = rows[0]["STATUS"]?.ToString();
        if (status != "IssuedOut" && status != "PartiallyIssued")
        {
            return Conflict(new { message = "Liệu này không ở trạng thái đang out (phải đang IssuedOut hoặc PartiallyIssued)." });
        }

        var balance = Convert.ToDecimal(rows[0]["BALANCE"]);
        var arrivalQty = Convert.ToDecimal(rows[0]["ARRIVALQTY"]);
        var newBalance = balance + req.Qty;
        if (newBalance > arrivalQty)
        {
            return BadRequest(new { message = $"Số lượng nhận lại ({req.Qty}) vượt quá số lượng đã xuất còn lại (tối đa {arrivalQty - balance})." });
        }

        var newStatus = newBalance >= arrivalQty ? "InStock" : "PartiallyIssued";

        if (!await LocationExistsAsync(req.LocationId))
        {
            return BadRequest(new { message = "Vị trí kệ không hợp lệ hoặc không còn hoạt động." });
        }

        var statements = new (string Sql, OracleParameter[] Parameters)[]
        {
            ("UPDATE PMC_Materials " +
             "   SET Balance = :NewBalance, Status = :NewStatus, CurrentLocationId = :LocationId, IsOverdue = 0, " +
             "       UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1 " +
             " WHERE MaterialId = :MaterialId AND Status IN ('IssuedOut', 'PartiallyIssued') AND Balance = :OldBalance",
             new[]
             {
                 new OracleParameter("NewBalance", newBalance),
                 new OracleParameter("NewStatus", newStatus),
                 new OracleParameter("LocationId", req.LocationId),
                 new OracleParameter("UserId", req.UserId),
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("OldBalance", balance),
             }),
            ("INSERT INTO PMC_StockMovements (MaterialId, MovementType, Qty, LocationId, UserId) " +
             "VALUES (:MaterialId, 'Return', :Qty, :LocationId, :UserId)",
             new[]
             {
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("Qty", req.Qty),
                 new OracleParameter("LocationId", req.LocationId),
                 new OracleParameter("UserId", req.UserId),
             }),
        };

        try
        {
            await _db.ExecuteBatchAsync(statements);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict(new { message = "Số lượng đã xuất của liệu này vừa bị thay đổi bởi thao tác khác, vui lòng tải lại và thử lại." });
        }

        return Ok();
    }

    /// <summary>
    /// Danh sách liệu có thể hủy — mọi liệu chưa hủy, chưa bị xoá mềm.
    /// </summary>
    [HttpGet("disposable")]
    public async Task<ActionResult<List<MaterialListItem>>> Disposable()
    {
        var rows = await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0
                 AND m.Status <> 'Disposed'
               ORDER BY m.CreatedAt DESC");

        return Ok(rows.Select(MapMaterialListItem).ToList());
    }

    /// <summary>
    /// Hủy liệu: chuyển Status='Disposed', Balance=0, ghi DisposedAt (bắt đầu đếm 30 ngày trước khi ẩn),
    /// ghi StockMovements (Dispose). Update Materials + insert Movement chạy CÙNG 1 transaction.
    /// </summary>
    [HttpPost("{id:int}/dispose")]
    public async Task<IActionResult> Dispose(int id, [FromBody] DisposeRequest req)
    {
        var rows = (await _db.QueryAsync(
            "SELECT Balance, Status FROM PMC_Materials WHERE MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        var status = rows[0]["STATUS"]?.ToString();
        if (status == "Disposed")
        {
            return Conflict(new { message = "Liệu này đã bị hủy trước đó." });
        }

        var balance = rows[0]["BALANCE"] != null ? Convert.ToDecimal(rows[0]["BALANCE"]) : 0m;

        var statements = new (string Sql, OracleParameter[] Parameters)[]
        {
            ("UPDATE PMC_Materials " +
             "   SET Status = 'Disposed', Balance = 0, CurrentLocationId = NULL, DisposedAt = SYSTIMESTAMP, " +
             "       UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1 " +
             " WHERE MaterialId = :MaterialId AND Status <> 'Disposed'",
             new[]
             {
                 new OracleParameter("UserId", req.UserId),
                 new OracleParameter("MaterialId", id),
             }),
            ("INSERT INTO PMC_StockMovements (MaterialId, MovementType, Qty, UserId) " +
             "VALUES (:MaterialId, 'Dispose', :Qty, :UserId)",
             new[]
             {
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("Qty", balance),
                 new OracleParameter("UserId", req.UserId),
             }),
        };

        try
        {
            await _db.ExecuteBatchAsync(statements);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict(new { message = "Liệu này vừa được người khác hủy, vui lòng tải lại danh sách." });
        }

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
            if (row.ArrivalQty is null || row.ArrivalQty <= 0)
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
                try
                {
                    result.InsertedCount = await _db.ExecuteBatchAsync(statements);
                }
                catch (Exception ex)
                {
                    // Lỗi DB (VD dữ liệu 1 cột dài hơn giới hạn VARCHAR2) không được để lộ nguyên
                    // stack trace .NET ra ngoài — trả message ngắn gọn, dễ hiểu cho người dùng Web.
                    return Conflict(new
                    {
                        message = "Không thể lưu batch này vào CSDL — có thể do 1 dòng có dữ liệu dài hơn giới hạn cho phép của 1 cột nào đó. Chi tiết lỗi DB: " + ex.Message,
                    });
                }
            }
        }

        return Ok(result);
    }

    private async Task<bool> LocationExistsAsync(int locationId)
    {
        var rows = await _db.QueryAsync(
            "SELECT 1 FROM PMC_StorageLocations WHERE LocationId = :LocationId AND IsActive = 1",
            new OracleParameter("LocationId", locationId));
        return rows.Any();
    }

    private async Task<bool> RecipientExistsAsync(int recipientId)
    {
        var rows = await _db.QueryAsync(
            "SELECT 1 FROM PMC_Recipients WHERE RecipientId = :RecipientId AND IsActive = 1",
            new OracleParameter("RecipientId", recipientId));
        return rows.Any();
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
        MatlDescription = row["MATLDESCRIPTION"]?.ToString(),
        ColorCode = row["COLORCODE"]?.ToString(),
        ArrivalQty = row["ARRIVALQTY"] != null ? Convert.ToDecimal(row["ARRIVALQTY"]) : null,
        Balance = row["BALANCE"] != null ? Convert.ToDecimal(row["BALANCE"]) : null,
        Unit = row["UNIT"]?.ToString(),
        Status = row["STATUS"]?.ToString() ?? string.Empty,
        LocationCode = row["LOCATIONCODE"]?.ToString(),
        IsOverdue = row["ISOVERDUE"] != null && Convert.ToInt32(row["ISOVERDUE"]) == 1,
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
        RequestOn = row["REQUESTON"] != null ? Convert.ToDateTime(row["REQUESTON"]) : null,
        MatlType = row["MATLTYPE"]?.ToString(),
        Pic = row["PIC"]?.ToString(),
        Mat = row["MAT"]?.ToString(),
        Balance = row["BALANCE"] != null ? Convert.ToDecimal(row["BALANCE"]) : null,
        Status = row["STATUS"]?.ToString() ?? string.Empty,
        LocationCode = row["LOCATIONCODE"]?.ToString(),
        StockedInAt = row["STOCKEDINAT"] != null ? Convert.ToDateTime(row["STOCKEDINAT"]) : null,
        LastIssuedAt = row["LASTISSUEDAT"] != null ? Convert.ToDateTime(row["LASTISSUEDAT"]) : null,
        DisposedAt = row["DISPOSEDAT"] != null ? Convert.ToDateTime(row["DISPOSEDAT"]) : null,
        IsOverdue = row["ISOVERDUE"] != null && Convert.ToInt32(row["ISOVERDUE"]) == 1,
        CreatedAt = Convert.ToDateTime(row["CREATEDAT"]),
        UpdatedAt = row["UPDATEDAT"] != null ? Convert.ToDateTime(row["UPDATEDAT"]) : null,
    };

    private static MovementHistoryItem MapMovementHistoryItem(Dictionary<string, object?> row) => new()
    {
        MovementId = Convert.ToInt32(row["MOVEMENTID"]),
        MovementType = row["MOVEMENTTYPE"]?.ToString() ?? string.Empty,
        Qty = Convert.ToDecimal(row["QTY"]),
        LocationCode = row["LOCATIONCODE"]?.ToString(),
        Username = row["USERNAME"]?.ToString(),
        RecipientName = row["RECIPIENTNAME"]?.ToString(),
        OccurredAt = Convert.ToDateTime(row["OCCURREDAT"]),
        Note = row["NOTE"]?.ToString(),
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
        new OracleParameter("RequestOn", (object?)r.RequestOn ?? DBNull.Value),
        new OracleParameter("MatlType", (object?)r.MatlType ?? DBNull.Value),
        new OracleParameter("Pic", (object?)r.Pic ?? DBNull.Value),
        new OracleParameter("Mat", (object?)r.Mat ?? DBNull.Value),
    };
}
