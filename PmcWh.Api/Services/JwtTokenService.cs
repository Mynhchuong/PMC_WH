using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PmcWh.Api.Services;

/// <summary>
/// Sinh token đăng nhập cho app mobile (Web gọi Api trực tiếp server-side, không cần token này).
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateToken(int userId, string username, string role, out DateTime expiresAt)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key.");
        var issuer = _config["Jwt:Issuer"] ?? "PmcWh";
        var audience = _config["Jwt:Audience"] ?? "PmcWhMobile";
        var expiryMinutes = _config.GetValue<int?>("Jwt:ExpiryMinutes") ?? 720;
        expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
        };

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt, signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
