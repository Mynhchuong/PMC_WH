using Microsoft.AspNetCore.Mvc;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly OracleDataService _oracleDataService;

    public HealthController(OracleDataService oracleDataService)
    {
        _oracleDataService = oracleDataService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var result = await _oracleDataService.QueryAsync("SELECT SYSDATE AS current_time FROM dual");
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }
}
