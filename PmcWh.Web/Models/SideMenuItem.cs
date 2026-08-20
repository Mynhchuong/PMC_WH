namespace PmcWh.Web.Models;

public class SideMenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Key tra trong wwwroot/js/warehouse-i18n.js (data-i18n) để đổi song ngữ — để trống nếu chưa cần dịch.</summary>
    public string? TranslationKey { get; set; }
    public string Icon { get; set; } = string.Empty;
    /// <summary>Cặp màu gradient riêng cho icon của mục này (VD "#0d6efd", "#0a58ca") — để trống thì
    /// dùng màu xám/đen mặc định. Dùng ở cả icon sidebar (_SideNav.cshtml) và icon header của
    /// chính trang đó, để 1 nhóm chức năng luôn có cùng 1 màu nhận diện xuyên suốt.</summary>
    public string? ColorFrom { get; set; }
    public string? ColorTo { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public object? RouteValues { get; set; }
    public List<SideMenuItem> Children { get; set; } = new();
    public Func<bool>? VisibleWhen { get; set; }
}
