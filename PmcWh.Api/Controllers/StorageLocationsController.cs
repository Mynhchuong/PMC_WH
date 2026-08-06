using Microsoft.AspNetCore.Mvc;
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
    /// Toàn bộ ô kệ đang hoạt động (198 ô theo layout thật) — dùng cho combobox lúc quét lên kệ.
    /// Danh sách cố định, không cần phân trang.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StorageLocationDto>>> Get()
    {
        var rows = await _db.QueryAsync(
            "SELECT LocationId, RackNo, LevelNo, Code FROM PMC_StorageLocations WHERE IsActive = 1 ORDER BY RackNo, LevelNo");

        var items = rows.Select(row => new StorageLocationDto
        {
            LocationId = Convert.ToInt32(row["LOCATIONID"]),
            RackNo = Convert.ToInt32(row["RACKNO"]),
            LevelNo = Convert.ToInt32(row["LEVELNO"]),
            Code = row["CODE"]?.ToString() ?? string.Empty,
        }).ToList();

        return Ok(items);
    }
}
