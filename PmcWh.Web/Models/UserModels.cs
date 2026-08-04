using System.ComponentModel.DataAnnotations;

namespace PmcWh.Web.Models;

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserListViewModel
{
    public List<UserDto> Items { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
    public CreateUserViewModel NewUser { get; set; } = new();
}

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Họ tên")]
    public string? FullName { get; set; }

    [Required]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "Member";
}
