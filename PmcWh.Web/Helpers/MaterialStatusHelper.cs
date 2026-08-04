namespace PmcWh.Web.Helpers;

/// <summary>
/// PMC_Materials.Status lưu bằng mã tiếng Anh (Staging/InStock/...) theo đúng CHECK constraint trong DB.
/// Helper này chỉ đổi sang tiếng Việt để hiển thị cho người dùng (kể cả công nhân kho) — không đổi giá trị lưu/lọc.
/// </summary>
public static class MaterialStatusHelper
{
    public static readonly string[] AllStatuses = { "Staging", "InStock", "PartiallyIssued", "IssuedOut", "Disposed" };

    public static string Label(string status) => status switch
    {
        "Staging" => "Đang chờ lên kệ",
        "InStock" => "Trong kho",
        "PartiallyIssued" => "Đã xuất 1 phần",
        "IssuedOut" => "Đã xuất hết",
        "Disposed" => "Đã hủy",
        _ => status,
    };

    public static (string TextClass, string Style) BadgeStyle(string status) => status switch
    {
        "Staging" => ("text-secondary", "background-color:rgba(100,116,139,0.14);"),
        "InStock" => ("text-success", "background-color:rgba(25,135,84,0.12);"),
        "PartiallyIssued" => ("text-warning", "background-color:rgba(255,193,7,0.18);"),
        "IssuedOut" => ("text-danger", "background-color:rgba(220,53,69,0.12);"),
        "Disposed" => ("text-white", "background-color:#495057;"),
        _ => ("text-secondary", "background-color:rgba(100,116,139,0.14);"),
    };
}
