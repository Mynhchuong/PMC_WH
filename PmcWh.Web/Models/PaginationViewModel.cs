namespace PmcWh.Web.Models;

public class PaginationViewModel
{
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;

    /// <summary>Các query string khác cần giữ nguyên khi đổi trang (từ khoá tìm kiếm, filter...).</summary>
    public Dictionary<string, string?> RouteValues { get; set; } = new();
}
