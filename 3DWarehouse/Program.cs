using Warehouse3D.Services;

var builder = WebApplication.CreateBuilder(args);

// Không đăng ký OracleDataService thẳng — mỗi kho (WarehouseRegistry) có 1 connection riêng,
// WarehouseController tự resolve đúng cái qua OracleDataServiceFactory.Create(warehouseId).
builder.Services.AddSingleton<OracleDataServiceFactory>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Index");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
