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

    // Balance = 0 lúc mới import — liệu đang "chờ lên kệ" (Staging) thì chưa có gì trong kho cả,
    // dù A.Q'TY đã biết. Balance chỉ bắt đầu = A.Q'TY khi Inbound (lên kệ) thật sự diễn ra.
    private const string InsertSql = @"
        INSERT INTO PMC_Materials
            (Barcode, CsCode, Dev, PoNo, Supplier, Model, Season, Stage, Colorway, Component,
             MatlDescription, ColorCode, ColorName, SizeSpec, ArrivalQty, Unit, FocFlag, ArrivalDate,
             Remark, Testing, TestRequire, TestQty, Category, RequestOn, MatlType, Pic, Mat,
             PoDate, Etd, OriginalPrice, PaymentPrice, Amount, Balance)
        VALUES
            (:Barcode, :CsCode, :Dev, :PoNo, :Supplier, :Model, :Season, :Stage, :Colorway, :Component,
             :MatlDescription, :ColorCode, :ColorName, :SizeSpec, :ArrivalQty, :Unit, :FocFlag, :ArrivalDate,
             :Remark, :Testing, :TestRequire, :TestQty, :Category, :RequestOn, :MatlType, :Pic, :Mat,
             :PoDate, :Etd, :OriginalPrice, :PaymentPrice, :Amount, 0)";

    private readonly OracleDataService _db;

    public MaterialsController(OracleDataService db)
    {
        _db = db;
    }

    /// <summary>
    /// Danh sách liệu (phân trang) — đủ cột mô tả (không chỉ 18 cột cốt lõi) để màn "Cơ sở dữ liệu
    /// PMC" hiện được nhiều cột hơn. Tìm theo [field] (mặc định "barcode", giữ tương thích tham số
    /// [barcode] kiểu cũ) trong [q], lọc thêm Status và khoảng Ngày Nhập (ArrivalDate).
    /// PMC cần lọc kết hợp nhiều cột cùng lúc (VD: tên liệu + color code + DEV) mới ra đúng kết quả
    /// vì data nhiều — [field2]/[q2] và [field3]/[q3] là 2 điều kiện AND thêm, đều optional.
    /// Luôn ẩn IsArchived=1 (đã xoá mềm).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MaterialListItem>>> Get(
        string? barcode, string? field, string? q, string? field2, string? q2, string? field3, string? q3,
        string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue, int page = 1, int pageSize = 20)
    {
        var searchValue = !string.IsNullOrWhiteSpace(q) ? q : barcode;
        var (searchSql, searchParams) = BuildSearchFilter(field, searchValue, field2, q2, field3, q3);

        var innerSql = $@"
            SELECT m.MaterialId, m.Barcode, m.CsCode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Season, m.Stage,
                   m.Colorway, m.Component, m.MatlDescription, m.ColorCode, m.ColorName, m.SizeSpec,
                   m.ArrivalQty, m.Balance, m.Unit, m.FocFlag, m.Remark, m.Testing, m.TestRequire, m.TestQty,
                   m.Category, m.RequestOn, m.MatlType, m.Pic, m.Mat,
                   m.PoDate, m.Etd, m.OriginalPrice, m.PaymentPrice, m.Amount,
                   m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt,
                   m.StockedInAt, m.LastIssuedAt, m.DisposedAt, m.UpdatedAt
              FROM PMC_Materials m
              LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
             WHERE m.IsArchived = 0
               {searchSql}
               AND (:status IS NULL OR m.Status = :status)
               AND (:fromDate IS NULL OR m.ArrivalDate >= :fromDate)
               AND (:toDate IS NULL OR m.ArrivalDate < :toDate + 1)
               AND (:isOverdue IS NULL OR m.IsOverdue = :isOverdue)
             ORDER BY m.CreatedAt DESC";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize,
            searchParams.Concat(new[]
            {
                new OracleParameter("status", (object?)status ?? DBNull.Value),
                new OracleParameter("fromDate", OracleDbType.Date) { Value = (object?)fromDate ?? DBNull.Value },
                new OracleParameter("toDate", OracleDbType.Date) { Value = (object?)toDate ?? DBNull.Value },
                new OracleParameter("isOverdue", (object?)(isOverdue.HasValue ? (isOverdue.Value ? 1 : 0) : null) ?? DBNull.Value),
            }).ToArray());

        var items = paged.Items.Select(MapMaterialListItemFull).ToList();

        return Ok(new PagedResult<MaterialListItem>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    /// <summary>
    /// Whitelist cột cho phép tìm (Index + Export) — KHÔNG được ghép tên cột trực tiếp từ client
    /// vào SQL, vì Oracle không bind được tên cột qua OracleParameter, chỉ chọn 1 trong các literal
    /// cố định sẵn ở đây (tránh SQL injection qua tên cột).
    /// </summary>
    private static string SearchColumnFor(string? field) => field?.Trim().ToLowerInvariant() switch
    {
        "dev" => "m.Dev",
        "po" => "m.PoNo",
        "supplier" => "m.Supplier",
        "model" => "m.Model",
        "season" => "m.Season",
        "stage" => "m.Stage",
        "matldescription" => "m.MatlDescription",
        "colorcode" => "m.ColorCode",
        "category" => "m.Category",
        "matltype" => "m.MatlType",
        "pic" => "m.Pic",
        "remark" => "m.Remark",
        "colorway" => "m.Colorway",
        "sizespec" => "m.SizeSpec",
        _ => "m.Barcode",
    };

    /// <summary>
    /// Điều kiện tìm kết hợp tối đa 3 cột cùng lúc (field/q, field2/q2, field3/q3, đều optional,
    /// AND với nhau) — PMC cần lọc nhiều cột 1 lượt vì data nhiều, lọc 1 cột chưa ra đúng kết quả.
    /// Dùng chung cho MỌI endpoint trả danh sách liệu (Get, Export, Issuable, Disposable,
    /// Returnable) thay vì lặp lại cùng 1 khối SQL + OracleParameter ở từng nơi.
    /// Ghép <paramref name="sql"/> vào ngay sau điều kiện WHERE hiện có của endpoint, rồi nối thêm
    /// <paramref name="parameters"/> vào mảng OracleParameter của câu QueryAsync/QueryPagedAsync đó.
    /// </summary>
    private static (string Sql, OracleParameter[] Parameters) BuildSearchFilter(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3)
    {
        var sql = $@"
               AND (:q IS NULL OR UPPER({SearchColumnFor(field)}) LIKE '%' || UPPER(:q) || '%')
               AND (:q2 IS NULL OR UPPER({SearchColumnFor(field2)}) LIKE '%' || UPPER(:q2) || '%')
               AND (:q3 IS NULL OR UPPER({SearchColumnFor(field3)}) LIKE '%' || UPPER(:q3) || '%')";

        var parameters = new[]
        {
            new OracleParameter("q", (object?)(string.IsNullOrWhiteSpace(q) ? null : q) ?? DBNull.Value),
            new OracleParameter("q2", (object?)(string.IsNullOrWhiteSpace(q2) ? null : q2) ?? DBNull.Value),
            new OracleParameter("q3", (object?)(string.IsNullOrWhiteSpace(q3) ? null : q3) ?? DBNull.Value),
        };

        return (sql, parameters);
    }

    /// <summary>
    /// Xuất Excel: cùng bộ lọc như danh sách nhưng KHÔNG phân trang, trả đủ mọi field mô tả
    /// (giống hệt cột trong file import PMC) — giới hạn an toàn 20000 dòng (PMC hiện có ~11k dòng
    /// data thật, để dư phòng tăng trưởng).
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult<List<MaterialDetail>>> Export(
        string? barcode, string? field, string? q, string? field2, string? q2, string? field3, string? q3,
        string? status, DateTime? fromDate, DateTime? toDate, bool? isOverdue)
    {
        var searchValue = !string.IsNullOrWhiteSpace(q) ? q : barcode;
        var (searchSql, searchParams) = BuildSearchFilter(field, searchValue, field2, q2, field3, q3);

        var sql = $@"
            SELECT * FROM (
                SELECT m.MaterialId, m.Barcode, m.CsCode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Season, m.Stage,
                       m.Colorway, m.Component, m.MatlDescription, m.ColorCode, m.ColorName, m.SizeSpec,
                       m.ArrivalQty, m.Unit, m.FocFlag, m.ArrivalDate, m.Remark, m.Testing, m.TestRequire,
                       m.TestQty, m.Category, m.RequestOn, m.MatlType, m.Pic, m.Mat,
                       m.PoDate, m.Etd, m.OriginalPrice, m.PaymentPrice, m.Amount,
                       m.Balance, m.Status, l.Code AS LocationCode, m.StockedInAt, m.LastIssuedAt,
                       m.DisposedAt, m.IsOverdue, m.CreatedAt, m.UpdatedAt
                  FROM PMC_Materials m
                  LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
                 WHERE m.IsArchived = 0
                   {searchSql}
                   AND (:status IS NULL OR m.Status = :status)
                   AND (:fromDate IS NULL OR m.ArrivalDate >= :fromDate)
                   AND (:toDate IS NULL OR m.ArrivalDate < :toDate + 1)
                   AND (:isOverdue IS NULL OR m.IsOverdue = :isOverdue)
                 ORDER BY m.CreatedAt DESC
            ) WHERE ROWNUM <= 20000";

        var rows = await _db.QueryAsync(sql,
            searchParams.Concat(new[]
            {
                new OracleParameter("status", (object?)status ?? DBNull.Value),
                new OracleParameter("fromDate", OracleDbType.Date) { Value = (object?)fromDate ?? DBNull.Value },
                new OracleParameter("toDate", OracleDbType.Date) { Value = (object?)toDate ?? DBNull.Value },
                new OracleParameter("isOverdue", (object?)(isOverdue.HasValue ? (isOverdue.Value ? 1 : 0) : null) ?? DBNull.Value),
            }).ToArray());

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
                     m.MatlType, m.Pic, m.Mat, m.PoDate, m.Etd, m.OriginalPrice, m.PaymentPrice, m.Amount,
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

        // Staging (chưa lên kệ): Balance luôn = 0 bất kể sửa A.Q'TY thế nào — chưa có gì thật sự
        // trong kho để mà cộng/trừ theo chênh lệch cả. Balance chỉ bắt đầu = A.Q'TY khi Inbound.
        decimal? newBalance;
        if (status == "Staging")
        {
            newBalance = 0m;
        }
        else
        {
            var delta = req.ArrivalQty.Value - oldArrivalQty;
            newBalance = oldBalance.HasValue ? oldBalance.Value + delta : (decimal?)null;

            if (oldBalance.HasValue && newBalance!.Value < 0)
            {
                return BadRequest(new
                {
                    message = $"Không thể sửa số lượng xuống {req.ArrivalQty.Value} — liệu đã xuất {oldArrivalQty - oldBalance.Value}, số lượng mới phải >= số đã xuất.",
                });
            }
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
                     PoDate = :PoDate, Etd = :Etd, OriginalPrice = :OriginalPrice,
                     PaymentPrice = :PaymentPrice, Amount = :Amount,
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
            new OracleParameter("PoDate", OracleDbType.Date) { Value = (object?)req.PoDate ?? DBNull.Value },
            new OracleParameter("Etd", OracleDbType.Date) { Value = (object?)req.Etd ?? DBNull.Value },
            new OracleParameter("OriginalPrice", (object?)req.OriginalPrice ?? DBNull.Value),
            new OracleParameter("PaymentPrice", (object?)req.PaymentPrice ?? DBNull.Value),
            new OracleParameter("Amount", (object?)req.Amount ?? DBNull.Value),
            new OracleParameter("UserId", req.UserId),
            new OracleParameter("MaterialId", id));

        if (affected == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        return Ok();
    }

    /// <summary>
    /// Xoá vĩnh viễn 1 liệu khỏi Oracle (DELETE FROM PMC_Materials) — theo yêu cầu PMC "xoá thì xoá
    /// luôn, không cần giữ lại trên hệ thống", không phải xoá mềm. Không thể khôi phục sau khi xoá.
    /// Xoá kèm lịch sử di chuyển (PMC_StockMovements) của liệu trước để không vướng ràng buộc khóa
    /// ngoại — minAffectedRowsPerStatement=0 vì liệu chưa từng lên kệ/xuất sẽ không có dòng lịch sử
    /// nào (0 dòng bị ảnh hưởng ở câu xoá StockMovements là hợp lệ, không phải lỗi).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var statements = new (string Sql, OracleParameter[] Parameters)[]
        {
            ("DELETE FROM PMC_StockMovements WHERE MaterialId = :MaterialId", new[] { new OracleParameter("MaterialId", id) }),
            ("DELETE FROM PMC_Materials WHERE MaterialId = :MaterialId", new[] { new OracleParameter("MaterialId", id) }),
        };

        var totalAffected = await _db.ExecuteBatchAsync(statements, minAffectedRowsPerStatement: 0);
        if (totalAffected == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu, hoặc liệu này đã bị xoá trước đó." });
        }

        return Ok();
    }

    /// <summary>
    /// Danh sách liệu đang xuất quá 90 ngày chưa nhận lại (IsOverdue=1, cả xuất 1 phần lẫn xuất hết) —
    /// PMC đi kiểm tra thực tế: còn liệu thì để đó, hết liệu thì hủy (dùng luôn action Dispose).
    /// SELECT đủ cột mô tả liệu giống hệt danh sách chính (Get) — page Web dùng lại đúng partial
    /// hiện cột (_MaterialColCells) cho nhất quán giữa các màn danh sách liệu.
    /// </summary>
    [HttpGet("overdue-issued")]
    public async Task<ActionResult<PagedResult<OverdueIssuedItem>>> OverdueIssued(int page = 1, int pageSize = 10)
    {
        var innerSql = @"
            SELECT m.MaterialId, m.Barcode, m.CsCode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Season, m.Stage,
                   m.Colorway, m.Component, m.MatlDescription, m.ColorCode, m.ColorName, m.SizeSpec,
                   m.ArrivalQty, m.Balance, m.Unit, m.FocFlag, m.Remark, m.Testing, m.TestRequire, m.TestQty,
                   m.Category, m.RequestOn, m.MatlType, m.Pic, m.Mat,
                   m.PoDate, m.Etd, m.OriginalPrice, m.PaymentPrice, m.Amount,
                   m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt,
                   m.StockedInAt, m.LastIssuedAt, m.DisposedAt, m.UpdatedAt,
                   TRUNC(SYSDATE) - TRUNC(m.LastIssuedAt) AS DaysOut,
                   (SELECT r.Name
                      FROM PMC_StockMovements mv
                      JOIN PMC_Recipients r ON r.RecipientId = mv.RecipientId
                     WHERE mv.MaterialId = m.MaterialId
                       AND mv.MovementType = 'IssueToWorkshop'
                       AND mv.MovementId = (
                             SELECT MAX(mv2.MovementId)
                               FROM PMC_StockMovements mv2
                              WHERE mv2.MaterialId = m.MaterialId AND mv2.MovementType = 'IssueToWorkshop'
                           )
                   ) AS RecipientName
              FROM PMC_Materials m
              LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
             WHERE m.IsOverdue = 1 AND m.IsArchived = 0
               AND m.Status IN ('IssuedOut', 'PartiallyIssued')
             ORDER BY m.LastIssuedAt NULLS FIRST";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize);
        var items = paged.Items.Select(MapOverdueIssuedItemFull).ToList();

        return Ok(new PagedResult<OverdueIssuedItem>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    /// <summary>
    /// Tra 1 liệu theo đúng barcode (khớp tuyệt đối) — dùng cho màn quét (Inbound/Issue) tra cứu nhanh.
    /// </summary>
    [HttpGet("by-barcode/{barcode}")]
    public async Task<ActionResult<MaterialListItem>> GetByBarcode(string barcode)
    {
        var rows = (await _db.QueryAsync(
            @"SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.Season, m.Stage,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt,
                     pl.PrevLocationId AS PreviousLocationId, pll.Code AS PreviousLocationCode
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
                LEFT JOIN (
                    SELECT MaterialId, LocationId AS PrevLocationId FROM (
                        SELECT sm.MaterialId, sm.LocationId,
                               ROW_NUMBER() OVER (PARTITION BY sm.MaterialId ORDER BY sm.OccurredAt DESC, sm.MovementId DESC) AS rn
                          FROM PMC_StockMovements sm
                         WHERE sm.LocationId IS NOT NULL
                    ) WHERE rn = 1
                ) pl ON pl.MaterialId = m.MaterialId
                LEFT JOIN PMC_StorageLocations pll ON pll.LocationId = pl.PrevLocationId
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
    /// Danh sách liệu có thể xuất (InStock hoặc PartiallyIssued, còn Balance > 0). Cùng bộ lọc 3
    /// điều kiện kết hợp (field/q, field2/q2, field3/q3) như Get/Export — xem BuildSearchFilter().
    /// </summary>
    [HttpGet("issuable")]
    public async Task<ActionResult<PagedResult<MaterialListItem>>> Issuable(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3, int page = 1, int pageSize = 10)
    {
        var (searchSql, searchParams) = BuildSearchFilter(field, q, field2, q2, field3, q3);

        var innerSql = $@"
            SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.Season, m.Stage,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0
                 AND m.Status IN ('InStock', 'PartiallyIssued')
                 AND m.Balance > 0
                 {searchSql}
               ORDER BY m.LastIssuedAt NULLS FIRST, m.CreatedAt";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize, searchParams);

        return Ok(new PagedResult<MaterialListItem>
        {
            Items = paged.Items.Select(MapMaterialListItem).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
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
            "SELECT Balance, Status, CurrentLocationId, ArrivalQty FROM PMC_Materials WHERE MaterialId = :id",
            new OracleParameter("id", id))).ToList();

        if (rows.Count == 0)
        {
            return NotFound(new { message = "Không tìm thấy liệu." });
        }

        var status = rows[0]["STATUS"]?.ToString();
        var balance = Convert.ToDecimal(rows[0]["BALANCE"]);
        // "Xuất Kho Thẳng": liệu mới in tem (Staging, CHƯA lên kệ) cũng xuất được thẳng cho WS —
        // nhưng CHỈ khi xuất hết 1 lần (remaining <= 0 → IssuedOut). Không cho xuất 1 phần từ Staging,
        // vì phần còn lại sẽ kẹt ở trạng thái PartiallyIssued mà KHÔNG có CurrentLocationId (chưa từng
        // lên kệ) — vỡ giả định "PartiallyIssued luôn còn nằm trên 1 kệ nào đó" ở nghiệp vụ Nhận lại
        // (Return) và Bản đồ kho. Nếu cần xuất 1 phần thì phải lên kệ trước (Nhập kho) rồi mới xuất.
        //
        // Balance của liệu Staging LUÔN là 0 trong DB (chỉ được set = ArrivalQty lúc Nhập kho — xem
        // Inbound() ở trên) — nên số lượng "có thể xuất" của 1 liệu Staging phải lấy từ ArrivalQty,
        // không phải Balance (dùng nhầm Balance sẽ luôn báo "vượt quá tồn hiện tại (0)", chặn hết mọi
        // lần xuất thẳng — bug thật đã gặp khi đối chiếu với dữ liệu Staging thật trong DB).
        var isDirectFromStaging = status == "Staging";
        var arrivalQty = Convert.ToDecimal(rows[0]["ARRIVALQTY"]);
        var availableQty = isDirectFromStaging ? arrivalQty : balance;
        if (status != "InStock" && status != "PartiallyIssued" && !isDirectFromStaging)
        {
            return Conflict(new { message = "Liệu này không ở trạng thái có thể xuất (phải đang InStock, PartiallyIssued, hoặc Staging để xuất thẳng)." });
        }
        if (isDirectFromStaging && req.Qty < availableQty)
        {
            return BadRequest(new { message = $"Liệu chưa lên kệ (Staging) chỉ có thể xuất thẳng HẾT toàn bộ số lượng ({availableQty}) — nếu cần xuất 1 phần, vui lòng lên kệ trước." });
        }

        if (req.Qty > availableQty)
        {
            return BadRequest(new { message = $"Số lượng xuất ({req.Qty}) vượt quá tồn hiện tại ({availableQty})." });
        }

        var currentLocationId = rows[0]["CURRENTLOCATIONID"] != null ? Convert.ToInt32(rows[0]["CURRENTLOCATIONID"]) : (int?)null;

        if (!await RecipientExistsAsync(req.RecipientId))
        {
            return BadRequest(new { message = "Nơi nhận không hợp lệ hoặc không còn hoạt động." });
        }

        var remaining = availableQty - req.Qty;
        var newStatus = remaining <= 0 ? "IssuedOut" : "PartiallyIssued";

        var statements = new (string Sql, OracleParameter[] Parameters)[]
        {
            ("UPDATE PMC_Materials " +
             "   SET Balance = :Remaining, Status = :NewStatus, LastIssuedAt = SYSTIMESTAMP, " +
             "       CurrentLocationId = CASE WHEN :NewStatus = 'IssuedOut' THEN NULL ELSE CurrentLocationId END, " +
             "       UpdatedBy = :UserId, UpdatedAt = SYSTIMESTAMP, VersionNo = VersionNo + 1 " +
             " WHERE MaterialId = :MaterialId AND Status IN ('InStock', 'PartiallyIssued', 'Staging') AND Balance = :OldBalance",
             new[]
             {
                 new OracleParameter("Remaining", remaining),
                 new OracleParameter("NewStatus", newStatus),
                 new OracleParameter("UserId", req.UserId),
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("OldBalance", balance),
             }),
            ("INSERT INTO PMC_StockMovements (MaterialId, MovementType, Qty, LocationId, UserId, RecipientId) " +
             "VALUES (:MaterialId, 'IssueToWorkshop', :Qty, :LocationId, :UserId, :RecipientId)",
             new[]
             {
                 new OracleParameter("MaterialId", id),
                 new OracleParameter("Qty", req.Qty),
                 new OracleParameter("LocationId", (object?)currentLocationId ?? DBNull.Value),
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
    /// Danh sách liệu đang out (IssuedOut/PartiallyIssued) — có thể nhận lại (Return). Cùng bộ lọc
    /// 3 điều kiện kết hợp như Get/Export — xem BuildSearchFilter().
    /// </summary>
    [HttpGet("returnable")]
    public async Task<ActionResult<PagedResult<MaterialListItem>>> Returnable(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3, int page = 1, int pageSize = 10)
    {
        var (searchSql, searchParams) = BuildSearchFilter(field, q, field2, q2, field3, q3);

        var innerSql = $@"
            SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.Season, m.Stage,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0
                 AND m.Status IN ('IssuedOut', 'PartiallyIssued')
                 {searchSql}
               ORDER BY m.LastIssuedAt NULLS FIRST, m.CreatedAt";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize, searchParams);

        return Ok(new PagedResult<MaterialListItem>
        {
            Items = paged.Items.Select(MapMaterialListItem).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
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
            "SELECT Balance, ArrivalQty, Status, CurrentLocationId FROM PMC_Materials WHERE MaterialId = :id",
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

        // Chốt kệ để lên lại:
        //  - Có gửi LocationId  -> dùng kệ đó (IssuedOut bắt buộc; PartiallyIssued gửi khi muốn DỜI
        //    sang kệ khác — PMC feedback).
        //  - Không gửi + PartiallyIssued (chưa từng rời kệ) -> giữ nguyên kệ hiện tại.
        //  - Không gửi + IssuedOut (đã rời kệ hẳn) -> bắt buộc phải chọn kệ.
        var existingLocationId = rows[0]["CURRENTLOCATIONID"] != null ? Convert.ToInt32(rows[0]["CURRENTLOCATIONID"]) : (int?)null;
        int resolvedLocationId;
        if (req.LocationId.HasValue)
        {
            if (!await LocationExistsAsync(req.LocationId.Value))
            {
                return BadRequest(new { message = "Vị trí kệ không hợp lệ hoặc không còn hoạt động." });
            }
            resolvedLocationId = req.LocationId.Value;
        }
        else if (status == "PartiallyIssued" && existingLocationId.HasValue)
        {
            resolvedLocationId = existingLocationId.Value;
        }
        else
        {
            return BadRequest(new { message = "Liệu đã xuất hết khỏi kệ — vui lòng chọn kệ để lên lại." });
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
                 new OracleParameter("LocationId", resolvedLocationId),
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
                 new OracleParameter("LocationId", resolvedLocationId),
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
    /// Danh sách liệu có thể hủy — mọi liệu chưa hủy, chưa bị xoá mềm. Cùng bộ lọc 3 điều kiện kết
    /// hợp như Get/Export — xem BuildSearchFilter().
    /// </summary>
    [HttpGet("disposable")]
    public async Task<ActionResult<PagedResult<MaterialListItem>>> Disposable(
        string? field, string? q, string? field2, string? q2, string? field3, string? q3, int page = 1, int pageSize = 10)
    {
        var (searchSql, searchParams) = BuildSearchFilter(field, q, field2, q2, field3, q3);

        var innerSql = $@"
            SELECT m.MaterialId, m.Barcode, m.Dev, m.PoNo, m.Supplier, m.Model, m.Colorway, m.SizeSpec, m.MatlDescription, m.ColorCode,
                     m.Season, m.Stage,
                     m.ArrivalQty, m.Balance, m.Unit, m.Status, l.Code AS LocationCode, m.IsOverdue, m.ArrivalDate, m.CreatedAt
                FROM PMC_Materials m
                LEFT JOIN PMC_StorageLocations l ON l.LocationId = m.CurrentLocationId
               WHERE m.IsArchived = 0
                 AND m.Status <> 'Disposed'
                 {searchSql}
               ORDER BY m.CreatedAt DESC";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize, searchParams);

        return Ok(new PagedResult<MaterialListItem>
        {
            Items = paged.Items.Select(MapMaterialListItem).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
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
             "   SET Status = 'Disposed', Balance = 0, CurrentLocationId = NULL, DisposedAt = SYSTIMESTAMP, IsOverdue = 0, " +
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
            var lengthViolation = FindFieldLengthViolation(row);
            if (lengthViolation != null)
            {
                result.Skipped.Add(new MaterialImportSkip(barcode, lengthViolation));
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

    /// <summary>
    /// Giới hạn ký tự các cột NVARCHAR2/VARCHAR2 của PMC_Materials (đúng theo Oracle, xem
    /// USER_TAB_COLUMNS.CHAR_LENGTH) — validate trước khi insert thay vì để Oracle tự chặn.
    /// Vì ExecuteBatchAsync gộp cả batch vào 1 transaction, 1 dòng vượt giới hạn gây ORA-12899 sẽ
    /// làm ROLLBACK LUÔN cả batch (tới 40 dòng), kéo theo mọi dòng hợp lệ khác cũng không insert
    /// được — validate sớm để chỉ đúng dòng lỗi bị skip, các dòng còn lại trong batch vẫn import
    /// bình thường (từng gặp thật: 1 dòng cột SIZE dài 62 ký tự làm rớt nguyên 40 dòng).
    /// </summary>
    private static readonly (string Column, int MaxLength, Func<MaterialImportRow, string?> Get)[] FieldLengthLimits =
    {
        ("BARCODE", 30, r => r.Barcode),
        ("DEV", 50, r => r.Dev),
        ("PO", 40, r => r.PoNo),
        ("SUPPLIER", 100, r => r.Supplier),
        ("MODEL", 100, r => r.Model),
        ("SEASON", 10, r => r.Season),
        ("STAGE", 100, r => r.Stage),
        ("COLORWAY", 100, r => r.Colorway),
        ("COMPONENT", 200, r => r.Component),
        ("MAT'L DESCRIPTION", 200, r => r.MatlDescription),
        ("COLOR CODE", 150, r => r.ColorCode),
        ("COLOR NAME", 100, r => r.ColorName),
        ("SIZE", 50, r => r.SizeSpec),
        ("UNIT", 10, r => r.Unit),
        ("FOC/NON FOC", 10, r => r.FocFlag),
        ("REMARK", 500, r => r.Remark),
        ("TEST REQUIRE", 100, r => r.TestRequire),
        ("TEST Q'TY", 30, r => r.TestQty),
        ("CATEGORY", 50, r => r.Category),
        ("MAT'L TYPE", 100, r => r.MatlType),
        ("PIC", 100, r => r.Pic),
        ("MAT", 100, r => r.Mat),
    };

    private static string? FindFieldLengthViolation(MaterialImportRow row)
    {
        foreach (var (column, maxLength, get) in FieldLengthLimits)
        {
            var value = get(row);
            if (value != null && value.Length > maxLength)
            {
                return $"Cột {column} dài {value.Length} ký tự, vượt quá giới hạn {maxLength} ký tự";
            }
        }
        return null;
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
        Season = row["SEASON"]?.ToString(),
        Stage = row["STAGE"]?.ToString(),
        ArrivalQty = row["ARRIVALQTY"] != null ? Convert.ToDecimal(row["ARRIVALQTY"]) : null,
        Balance = row["BALANCE"] != null ? Convert.ToDecimal(row["BALANCE"]) : null,
        Unit = row["UNIT"]?.ToString(),
        Status = row["STATUS"]?.ToString() ?? string.Empty,
        LocationCode = row["LOCATIONCODE"]?.ToString(),
        // Null-safe: chỉ SQL của GetByBarcode SELECT 2 cột này; các endpoint khác dùng chung
        // MapMaterialListItem sẽ KHÔNG có key -> TryGetValue để khỏi ném KeyNotFoundException.
        PreviousLocationId = row.TryGetValue("PREVIOUSLOCATIONID", out var prevLocId) && prevLocId != null
            ? Convert.ToInt32(prevLocId) : null,
        PreviousLocationCode = row.TryGetValue("PREVIOUSLOCATIONCODE", out var prevLocCode)
            ? prevLocCode?.ToString() : null,
        IsOverdue = row["ISOVERDUE"] != null && Convert.ToInt32(row["ISOVERDUE"]) == 1,
        ArrivalDate = row["ARRIVALDATE"] != null ? Convert.ToDateTime(row["ARRIVALDATE"]) : null,
        CreatedAt = Convert.ToDateTime(row["CREATEDAT"]),
    };

    /// <summary>
    /// Bản mở rộng của MapMaterialListItem — thêm các trường mô tả còn lại (Season, Stage,
    /// Component...) CHỈ dùng cho danh sách chính (Get ở trên) vì đó là nơi duy nhất SELECT đủ các
    /// cột này. KHÔNG dùng hàm này cho GetByBarcode/Issuable/Returnable/Disposable... — SQL của
    /// các endpoint đó hẹp hơn, thiếu cột sẽ ném KeyNotFoundException.
    /// </summary>
    private static MaterialListItem MapMaterialListItemFull(Dictionary<string, object?> row)
    {
        var item = MapMaterialListItem(row);
        item.CsCode = row["CSCODE"] != null ? Convert.ToInt32(row["CSCODE"]) : null;
        item.Season = row["SEASON"]?.ToString();
        item.Stage = row["STAGE"]?.ToString();
        item.Component = row["COMPONENT"]?.ToString();
        item.ColorName = row["COLORNAME"]?.ToString();
        item.FocFlag = row["FOCFLAG"]?.ToString();
        item.Remark = row["REMARK"]?.ToString();
        item.Testing = row["TESTING"] != null ? Convert.ToInt32(row["TESTING"]) : null;
        item.TestRequire = row["TESTREQUIRE"]?.ToString();
        item.TestQty = row["TESTQTY"]?.ToString();
        item.Category = row["CATEGORY"]?.ToString();
        item.RequestOn = row["REQUESTON"] != null ? Convert.ToDateTime(row["REQUESTON"]) : null;
        item.MatlType = row["MATLTYPE"]?.ToString();
        item.Pic = row["PIC"]?.ToString();
        item.Mat = row["MAT"]?.ToString();
        item.PoDate = row["PODATE"] != null ? Convert.ToDateTime(row["PODATE"]) : null;
        item.Etd = row["ETD"] != null ? Convert.ToDateTime(row["ETD"]) : null;
        item.OriginalPrice = row["ORIGINALPRICE"] != null ? Convert.ToDecimal(row["ORIGINALPRICE"]) : null;
        item.PaymentPrice = row["PAYMENTPRICE"] != null ? Convert.ToDecimal(row["PAYMENTPRICE"]) : null;
        item.Amount = row["AMOUNT"] != null ? Convert.ToDecimal(row["AMOUNT"]) : null;
        item.StockedInAt = row["STOCKEDINAT"] != null ? Convert.ToDateTime(row["STOCKEDINAT"]) : null;
        item.LastIssuedAt = row["LASTISSUEDAT"] != null ? Convert.ToDateTime(row["LASTISSUEDAT"]) : null;
        item.DisposedAt = row["DISPOSEDAT"] != null ? Convert.ToDateTime(row["DISPOSEDAT"]) : null;
        item.UpdatedAt = row["UPDATEDAT"] != null ? Convert.ToDateTime(row["UPDATEDAT"]) : null;
        return item;
    }

    /// <summary>
    /// Giống hệt MapMaterialListItemFull nhưng trả OverdueIssuedItem (kế thừa MaterialListItem)
    /// kèm 2 cột riêng của màn Issues Overdue (RecipientName, DaysOut) — không tái dùng trực tiếp
    /// MapMaterialListItemFull được vì hàm đó trả kiểu MaterialListItem, không phải subclass.
    /// </summary>
    private static OverdueIssuedItem MapOverdueIssuedItemFull(Dictionary<string, object?> row) => new()
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
        Season = row["SEASON"]?.ToString(),
        Stage = row["STAGE"]?.ToString(),
        ArrivalQty = row["ARRIVALQTY"] != null ? Convert.ToDecimal(row["ARRIVALQTY"]) : null,
        Balance = row["BALANCE"] != null ? Convert.ToDecimal(row["BALANCE"]) : null,
        Unit = row["UNIT"]?.ToString(),
        Status = row["STATUS"]?.ToString() ?? string.Empty,
        LocationCode = row["LOCATIONCODE"]?.ToString(),
        IsOverdue = row["ISOVERDUE"] != null && Convert.ToInt32(row["ISOVERDUE"]) == 1,
        ArrivalDate = row["ARRIVALDATE"] != null ? Convert.ToDateTime(row["ARRIVALDATE"]) : null,
        CreatedAt = Convert.ToDateTime(row["CREATEDAT"]),
        CsCode = row["CSCODE"] != null ? Convert.ToInt32(row["CSCODE"]) : null,
        Component = row["COMPONENT"]?.ToString(),
        ColorName = row["COLORNAME"]?.ToString(),
        FocFlag = row["FOCFLAG"]?.ToString(),
        Remark = row["REMARK"]?.ToString(),
        Testing = row["TESTING"] != null ? Convert.ToInt32(row["TESTING"]) : null,
        TestRequire = row["TESTREQUIRE"]?.ToString(),
        TestQty = row["TESTQTY"]?.ToString(),
        Category = row["CATEGORY"]?.ToString(),
        RequestOn = row["REQUESTON"] != null ? Convert.ToDateTime(row["REQUESTON"]) : null,
        MatlType = row["MATLTYPE"]?.ToString(),
        Pic = row["PIC"]?.ToString(),
        Mat = row["MAT"]?.ToString(),
        PoDate = row["PODATE"] != null ? Convert.ToDateTime(row["PODATE"]) : null,
        Etd = row["ETD"] != null ? Convert.ToDateTime(row["ETD"]) : null,
        OriginalPrice = row["ORIGINALPRICE"] != null ? Convert.ToDecimal(row["ORIGINALPRICE"]) : null,
        PaymentPrice = row["PAYMENTPRICE"] != null ? Convert.ToDecimal(row["PAYMENTPRICE"]) : null,
        Amount = row["AMOUNT"] != null ? Convert.ToDecimal(row["AMOUNT"]) : null,
        StockedInAt = row["STOCKEDINAT"] != null ? Convert.ToDateTime(row["STOCKEDINAT"]) : null,
        LastIssuedAt = row["LASTISSUEDAT"] != null ? Convert.ToDateTime(row["LASTISSUEDAT"]) : null,
        DisposedAt = row["DISPOSEDAT"] != null ? Convert.ToDateTime(row["DISPOSEDAT"]) : null,
        UpdatedAt = row["UPDATEDAT"] != null ? Convert.ToDateTime(row["UPDATEDAT"]) : null,
        RecipientName = row["RECIPIENTNAME"]?.ToString(),
        DaysOut = row["DAYSOUT"] != null ? Convert.ToInt32(row["DAYSOUT"]) : 0,
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
        PoDate = row["PODATE"] != null ? Convert.ToDateTime(row["PODATE"]) : null,
        Etd = row["ETD"] != null ? Convert.ToDateTime(row["ETD"]) : null,
        OriginalPrice = row["ORIGINALPRICE"] != null ? Convert.ToDecimal(row["ORIGINALPRICE"]) : null,
        PaymentPrice = row["PAYMENTPRICE"] != null ? Convert.ToDecimal(row["PAYMENTPRICE"]) : null,
        Amount = row["AMOUNT"] != null ? Convert.ToDecimal(row["AMOUNT"]) : null,
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
        new OracleParameter("PoDate", (object?)r.PoDate ?? DBNull.Value),
        new OracleParameter("Etd", (object?)r.Etd ?? DBNull.Value),
        new OracleParameter("OriginalPrice", (object?)r.OriginalPrice ?? DBNull.Value),
        new OracleParameter("PaymentPrice", (object?)r.PaymentPrice ?? DBNull.Value),
        new OracleParameter("Amount", (object?)r.Amount ?? DBNull.Value),
    };
}
