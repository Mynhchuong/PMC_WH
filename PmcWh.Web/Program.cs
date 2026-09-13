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

// TempData (flash message + kết quả import) lưu server-side qua Session, KHÔNG dùng cookie mặc định.
// Import file nhiều dòng lỗi -> JSON danh sách dòng lỗi nhét vào cookie TempData làm header request
// vượt giới hạn của IIS/http.sys => trình duyệt nhận "HTTP 400 - request headers too long" ở lần
// request kế tiếp. Session chỉ gửi 1 cookie id nhỏ, payload nằm trong RAM server.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".PmcWh.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
mvcBuilder.AddSessionStateTempDataProvider();

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

// Dọn cookie TempData kiểu cũ (.AspNetCore.Mvc.CookieTempDataProvider*) còn sót lại trên trình
// duyệt của user TỪ TRƯỚC lúc đổi TempData sang Session (xem ghi chú AddSession ở trên) — cookie
// cũ này có thể đã bị chia hàng chục mảnh (chunks-52...), làm request sau đó vượt giới hạn header
// của IIS/http.sys => "HTTP 400 - request headers too long" NGAY CẢ VỚI TRANG LOGIN, vì IIS chặn
// request trước khi vào tới app. Middleware này chỉ dọn được cho user CHƯA vượt ngưỡng (request
// còn lọt qua IIS tới đây) — user đã bị chặn hẳn thì phải tự xoá cookie/site data 1 lần thủ công,
// dọn ở server không giúp được vì request của họ không bao giờ chạm tới dòng code này.
app.Use(async (context, next) =>
{
    var staleNames = context.Request.Cookies.Keys
        .Where(k => k.StartsWith(".AspNetCore.Mvc.CookieTempDataProvider", StringComparison.Ordinal))
        .ToList();
    foreach (var name in staleNames)
    {
        foreach (var path in new[] { "/", context.Request.PathBase.Value })
        {
            if (string.IsNullOrEmpty(path)) continue;
            context.Response.Cookies.Delete(name, new CookieOptions { Path = path });
        }
    }
    await next();
});

// Không dùng HTTPS redirect — IIS server nội bộ chưa có SSL cert, chỉ chạy HTTP.
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Materials}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<PmcWh.Web.Hubs.WarehouseHub>("/warehouseHub");


app.Run();
