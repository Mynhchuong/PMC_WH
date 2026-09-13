using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Helpers;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public UsersController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var paged = await client.GetFromJsonAsync<PagedResultDto<UserDto>>($"api/Users?page={page}&pageSize={pageSize}", ApiJsonOptions);

        var model = new UserListViewModel
        {
            Items = paged?.Items ?? new List<UserDto>(),
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = paged?.TotalCount ?? 0,
                TotalPages = paged?.TotalPages ?? 0,
                Controller = "Users",
                Action = "Index",
            },
        };

        if (TempData["FlashSuccess"] is string success) ViewData["FlashSuccess"] = success;
        if (TempData["FlashError"] is string error) ViewData["FlashError"] = error;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel newUser, int page = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid)
        {
            return await RenderIndexWithErrors(newUser, page, pageSize);
        }

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync("api/Users", newUser);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            ModelState.AddModelError(string.Empty, problem?.Message ?? "Không thể tạo người dùng.");
            return await RenderIndexWithErrors(newUser, page, pageSize);
        }

        TempData["FlashSuccess"] = FlashHelper.Msg("createdUserSuccess", newUser.Username);
        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int userId, string username, int page = 1, int pageSize = 10)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsync($"api/Users/{userId}/reset-password", null);

        TempData[response.IsSuccessStatusCode ? "FlashSuccess" : "FlashError"] = response.IsSuccessStatusCode
            ? FlashHelper.Msg("resetPasswordSuccess", username)
            : FlashHelper.Msg("resetPasswordFailFallback", username);

        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    /// <summary>Admin tự đặt mật khẩu mới theo ý (khác ResetPassword vốn luôn về "123456" cố định).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(int userId, string username, string newPassword, int page = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Trim().Length < 6)
        {
            TempData["FlashError"] = "Mật khẩu mới phải từ 6 ký tự trở lên.";
            return RedirectToAction(nameof(Index), new { page, pageSize });
        }

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsJsonAsync($"api/Users/{userId}/change-password", new { newPassword });

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("changedPasswordSuccess", username);
        }
        else
        {
            TempData["FlashError"] = await TryReadApiMessageAsync(response) ?? FlashHelper.Msg("changePasswordFailFallback", username);
        }

        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    /// <summary>Bật/tắt đăng nhập của 1 user (không xóa — lịch sử nhập/xuất cũ vẫn cần giữ tên
    /// người thực hiện). Chặn tự tắt chính mình — tránh tự khóa mất quyền truy cập ngay lập tức.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int userId, string username, int page = 1, int pageSize = 10)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (userId == currentUserId)
        {
            TempData["FlashError"] = "Không thể tự tắt tài khoản đang đăng nhập của chính mình.";
            return RedirectToAction(nameof(Index), new { page, pageSize });
        }

        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsync($"api/Users/{userId}/toggle-active", null);

        if (response.IsSuccessStatusCode)
        {
            TempData["FlashSuccess"] = FlashHelper.Msg("toggledUserSuccess", username);
        }
        else
        {
            TempData["FlashError"] = await TryReadApiMessageAsync(response) ?? FlashHelper.Msg("toggleUserFailFallback", username);
        }

        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    /// <summary>Đọc {message: "..."} từ response lỗi của Api một cách an toàn — 1 số lỗi (VD NotFound)
    /// trả về body rỗng, ReadFromJsonAsync ném JsonException nếu gọi thẳng trên body đó.</summary>
    private static async Task<string?> TryReadApiMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiMessage>(ApiJsonOptions);
            return problem?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<IActionResult> RenderIndexWithErrors(CreateUserViewModel newUser, int page, int pageSize)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var paged = await client.GetFromJsonAsync<PagedResultDto<UserDto>>($"api/Users?page={page}&pageSize={pageSize}", ApiJsonOptions);

        var model = new UserListViewModel
        {
            Items = paged?.Items ?? new List<UserDto>(),
            NewUser = newUser,
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = paged?.TotalCount ?? 0,
                TotalPages = paged?.TotalPages ?? 0,
                Controller = "Users",
                Action = "Index",
            },
        };
        return View(nameof(Index), model);
    }

    private class ApiMessage
    {
        public string? Message { get; set; }
    }
}
