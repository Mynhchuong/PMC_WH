using PmcWh.Api.Models;
using PmcWh.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OracleConnectionOptions>(builder.Configuration.GetSection(OracleConnectionOptions.SectionName));
builder.Services.AddSingleton<OracleDataService>();
builder.Services.AddSingleton<MaintenanceJobService>();
builder.Services.AddHostedService<MaintenanceBackgroundService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Không dùng HTTPS redirect — IIS server nội bộ chưa có SSL cert, chỉ chạy HTTP.
app.UseAuthorization();
app.MapControllers();

app.Run();
