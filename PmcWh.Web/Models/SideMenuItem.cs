namespace PmcWh.Web.Models;

public class SideMenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Key tra trong wwwroot/js/warehouse-i18n.js (data-i18n) để đổi song ngữ — để trống nếu chưa cần dịch.</summary>
    public string? TranslationKey { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public object? RouteValues { get; set; }
    public List<SideMenuItem> Children { get; set; } = new();
    public Func<bool>? VisibleWhen { get; set; }
}
