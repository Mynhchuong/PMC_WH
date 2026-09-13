namespace PmcWh.Api.Models;

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = "Member";
}

/// <summary>Body cho POST api/Users/{id}/change-password — admin tự đặt mật khẩu mới theo ý (khác
/// reset-password vốn luôn đặt về "123456" cố định).</summary>
public class ChangePasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
    /// <summary>Token cho app mobile (Bearer) — null nếu Success = false. Web không dùng field này.</summary>
    public string? Token { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}
