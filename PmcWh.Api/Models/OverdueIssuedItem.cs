namespace PmcWh.Api.Models;

/// <summary>
/// Kế thừa MaterialListItem để màn Issues Overdue 90+ Days (Web) hiện được đủ cột mô tả liệu
/// giống hệt các màn danh sách liệu khác (Materials/Index, Inbound/Index), thay vì chỉ 1 tập cột
/// hẹp riêng — xem MaterialsController.MapOverdueIssuedItemFull.
/// </summary>
public class OverdueIssuedItem : MaterialListItem
{
    public string? RecipientName { get; set; }
    public int DaysOut { get; set; }
}
