using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    // Every page requires login by default; opt out with [AllowAnonymous] (e.g. AccountController).
    options.Filters.Add(new AuthorizeFilter());
});
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// Ghi key vào 1 folder cố định trong app thay vì user profile mặc định — App Pool Identity trên
// IIS thường không có user profile load sẵn, thiếu dòng này thì SignInAsync() lúc Login sẽ ném
// exception (không mã hoá/ký được cookie). Cùng pattern với HR_web (đã chạy ổn trên server này).
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")));

builder.Services.AddHttpClient("PmcApi", client =>
{
    var baseUrl = builder.Configuration["PmcApi:BaseUrl"] ?? "http://localhost:5073";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddSignalR();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Không dùng UseHsts() — HSTS ép trình duyệt luôn đòi HTTPS cho domain này, sẽ làm mất
    // luôn quyền truy cập vì server chỉ chạy HTTP nội bộ, chưa có SSL cert.
}

// Không dùng HTTPS redirect — IIS server nội bộ chưa có SSL cert, chỉ chạy HTTP.
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Materials}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<PmcWh.Web.Hubs.WarehouseHub>("/warehouseHub");


app.Run();
