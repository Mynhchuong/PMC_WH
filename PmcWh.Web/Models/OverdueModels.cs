namespace PmcWh.Web.Models;

/// <summary>
/// Kế thừa MaterialListItem để Overdue/Index dùng lại được đúng partial hiện cột
/// (_MaterialColGroup/_MaterialColHeaders/_MaterialColCells) giống Materials/Index và
/// Inbound/Index — nhất quán cột thông tin giữa các màn danh sách liệu.
/// </summary>
public class OverdueIssuedItem : MaterialListItem
{
    public string? RecipientName { get; set; }
    public int DaysOut { get; set; }
}

public class OverdueViewModel
{
    public List<OverdueIssuedItem> Items { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
}
