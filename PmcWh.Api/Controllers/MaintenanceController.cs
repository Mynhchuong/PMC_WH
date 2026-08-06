using Microsoft.AspNetCore.Mvc;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly MaintenanceJobService _job;

    public MaintenanceController(MaintenanceJobService job)
    {
        _job = job;
    }

    /// <summary>Chạy tay 2 job bảo trì (đánh cờ quá 90 ngày + ẩn hàng hủy quá 30 ngày) — dùng để test/vận hành thủ công.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        var result = await _job.RunAsync();
        return Ok(result);
    }
}
