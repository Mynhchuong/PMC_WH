using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
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
    public async Task<IActionResult> Create(CreateUserViewModel newUser, int page = 1, int pageSize = 20)
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

        TempData["FlashSuccess"] = $"Đã tạo người dùng '{newUser.Username}'.";
        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int userId, string username, int page = 1, int pageSize = 20)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var response = await client.PostAsync($"api/Users/{userId}/reset-password", null);

        TempData[response.IsSuccessStatusCode ? "FlashSuccess" : "FlashError"] = response.IsSuccessStatusCode
            ? $"Đã đặt lại mật khẩu của '{username}' về '123456'."
            : $"Không thể đặt lại mật khẩu của '{username}'.";

        return RedirectToAction(nameof(Index), new { page, pageSize });
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
