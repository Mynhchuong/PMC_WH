using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PmcWh.Api.Models;
using PmcWh.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OracleConnectionOptions>(builder.Configuration.GetSection(OracleConnectionOptions.SectionName));
builder.Services.AddSingleton<OracleDataService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<MaintenanceJobService>();
builder.Services.AddHostedService<MaintenanceBackgroundService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Token cho app mobile (Web vẫn gọi Api trực tiếp không token như cũ — chỉ mobile dùng đường này).
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key (User Secrets ở Development, appsettings.Production.json ở Production).");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PmcWh";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PmcWhMobile";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Không dùng HTTPS redirect — IIS server nội bộ chưa có SSL cert, chỉ chạy HTTP.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
