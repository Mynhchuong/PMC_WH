using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipientsController : ControllerBase
{
    private readonly OracleDataService _db;

    public RecipientsController(OracleDataService db)
    {
        _db = db;
    }

    /// <summary>
    /// Danh sách nơi nhận (combobox lúc out hàng). Mặc định chỉ lấy IsActive=1,
    /// truyền includeInactive=true để admin xem cả nơi đã tắt.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<RecipientDto>>> Get(bool includeInactive = false, int page = 1, int pageSize = 50)
    {
        var innerSql = includeInactive
            ? "SELECT RecipientId, Name, IsActive FROM PMC_Recipients ORDER BY Name"
            : "SELECT RecipientId, Name, IsActive FROM PMC_Recipients WHERE IsActive = 1 ORDER BY Name";

        var paged = await _db.QueryPagedAsync(innerSql, page, pageSize);
        var items = paged.Items.Select(MapRecipient).ToList();

        return Ok(new PagedResult<RecipientDto>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    [HttpPost]
    public async Task<ActionResult<RecipientDto>> Create([FromBody] CreateRecipientRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest(new { message = "Tên nơi nhận không được để trống." });
        }

        try
        {
            await _db.ExecuteAsync(
                "INSERT INTO PMC_Recipients (Name) VALUES (:Name)",
                new OracleParameter("Name", req.Name.Trim()));
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return Conflict(new { message = $"Nơi nhận '{req.Name}' đã tồn tại." });
        }

        var rows = await _db.QueryAsync(
            "SELECT RecipientId, Name, IsActive FROM PMC_Recipients WHERE Name = :Name",
            new OracleParameter("Name", req.Name.Trim()));

        return Ok(MapRecipient(rows.First()));
    }

    [HttpPost("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var affected = await _db.ExecuteAsync(
            "UPDATE PMC_Recipients SET IsActive = 1 - IsActive WHERE RecipientId = :Id",
            new OracleParameter("Id", id));

        if (affected == 0)
        {
            return NotFound();
        }

        return Ok();
    }

    private static RecipientDto MapRecipient(Dictionary<string, object?> row) => new()
    {
        RecipientId = Convert.ToInt32(row["RECIPIENTID"]),
        Name = row["NAME"]?.ToString() ?? string.Empty,
        IsActive = Convert.ToInt32(row["ISACTIVE"]) == 1,
    };
}
