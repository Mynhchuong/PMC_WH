using PmcWh.Api.Models;
using PmcWh.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OracleConnectionOptions>(builder.Configuration.GetSection(OracleConnectionOptions.SectionName));
builder.Services.AddSingleton<OracleDataService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
