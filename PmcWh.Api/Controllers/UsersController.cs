using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

namespace PmcWh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private const string DefaultResetPassword = "123456";

    private readonly OracleDataService _db;
    private readonly JwtTokenService _jwt;

    public UsersController(OracleDataService db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> Get(int page = 1, int pageSize = 20)
    {
        var paged = await _db.QueryPagedAsync(
            "SELECT UserId, Username, FullName, Role, IsActive, CreatedAt FROM PMC_Users ORDER BY UserId",
            page, pageSize);

        var items = paged.Items.Select(MapUser).ToList();

        return Ok(new PagedResult<UserDto>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
        });
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest(new { message = "Username và Password không được để trống." });
        }

        try
        {
            const string sql = @"
                INSERT INTO PMC_Users (Username, PasswordHash, FullName, Role)
                VALUES (:Username, :PasswordHash, :FullName, :Role)";

            await _db.ExecuteAsync(sql,
                new OracleParameter("Username", req.Username.Trim()),
                new OracleParameter("PasswordHash", req.Password),
                new OracleParameter("FullName", (object?)req.FullName ?? DBNull.Value),
                new OracleParameter("Role", req.Role));
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return Conflict(new { message = $"Username '{req.Username}' đã tồn tại." });
        }
        catch (OracleException ex) when (ex.Number == 2290)
        {
            return BadRequest(new { message = "Role không hợp lệ (chỉ 'Admin' hoặc 'Member')." });
        }

        var rows = await _db.QueryAsync(
            "SELECT UserId, Username, FullName, Role, IsActive, CreatedAt FROM PMC_Users WHERE Username = :Username",
            new OracleParameter("Username", req.Username.Trim()));

        return Ok(MapUser(rows.First()));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var affected = await _db.ExecuteAsync(
            "UPDATE PMC_Users SET PasswordHash = :PasswordHash WHERE UserId = :UserId",
            new OracleParameter("PasswordHash", DefaultResetPassword),
            new OracleParameter("UserId", id));

        if (affected == 0)
        {
            return NotFound();
        }

        return Ok(new { message = $"Đã đặt lại mật khẩu về '{DefaultResetPassword}'." });
    }

    [HttpPost("authenticate")]
    public async Task<ActionResult<LoginResult>> Authenticate([FromBody] LoginRequest req)
    {
        var rows = (await _db.QueryAsync(
            "SELECT UserId, Username, PasswordHash, FullName, Role, IsActive FROM PMC_Users WHERE Username = :Username",
            new OracleParameter("Username", req.Username?.Trim() ?? string.Empty))).ToList();

        if (rows.Count == 0)
        {
            return Ok(new LoginResult { Success = false, Message = "Tên đăng nhập hoặc mật khẩu không đúng." });
        }

        var row = rows[0];
        var isActive = Convert.ToInt32(row["ISACTIVE"]) == 1;
        var storedPassword = row["PASSWORDHASH"]?.ToString() ?? string.Empty;

        if (storedPassword != (req.Password ?? string.Empty) || !isActive)
        {
            return Ok(new LoginResult { Success = false, Message = "Tên đăng nhập hoặc mật khẩu không đúng." });
        }

        var userId = Convert.ToInt32(row["USERID"]);
        var username = row["USERNAME"]?.ToString() ?? string.Empty;
        var role = row["ROLE"]?.ToString() ?? "Member";
        var token = _jwt.GenerateToken(userId, username, role, out var expiresAt);

        return Ok(new LoginResult
        {
            Success = true,
            UserId = userId,
            Username = username,
            FullName = row["FULLNAME"]?.ToString(),
            Role = role,
            Token = token,
            TokenExpiresAt = expiresAt,
        });
    }

    private static UserDto MapUser(Dictionary<string, object?> row) => new()
    {
        UserId = Convert.ToInt32(row["USERID"]),
        Username = row["USERNAME"]?.ToString() ?? string.Empty,
        FullName = row["FULLNAME"]?.ToString(),
        Role = row["ROLE"]?.ToString() ?? string.Empty,
        IsActive = Convert.ToInt32(row["ISACTIVE"]) == 1,
        CreatedAt = Convert.ToDateTime(row["CREATEDAT"]),
    };
}
