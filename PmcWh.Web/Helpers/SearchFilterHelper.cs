namespace PmcWh.Web.Helpers;

/// <summary>
/// Build đoạn query string field/q/field2/q2/field3/q3 dùng chung cho MỌI Web controller gọi sang
/// Api (Materials, Inbound, Issue, Dispose) — khớp BuildSearchFilter() bên PmcWh.Api
/// MaterialsController, tránh lặp lại 6 lần Uri.EscapeDataString() ở từng nơi.
/// </summary>
public static class SearchFilterHelper
{
    public static string ToQueryString(string? field, string? q, string? field2, string? q2, string? field3, string? q3) =>
        $"field={Uri.EscapeDataString(field ?? string.Empty)}" +
        $"&q={Uri.EscapeDataString(q ?? string.Empty)}" +
        $"&field2={Uri.EscapeDataString(field2 ?? string.Empty)}" +
        $"&q2={Uri.EscapeDataString(q2 ?? string.Empty)}" +
        $"&field3={Uri.EscapeDataString(field3 ?? string.Empty)}" +
        $"&q3={Uri.EscapeDataString(q3 ?? string.Empty)}";
}
