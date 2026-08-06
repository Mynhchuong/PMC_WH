using PmcWh.Web.Models;

namespace PmcWh.Web.Helpers;

public static class SideMenuBuilder
{
    public static List<SideMenuItem> Build(bool isAdmin)
    {
        return new List<SideMenuItem>
        {
            new SideMenuItem
            {
                Id = "Materials",
                Title = "Liệu",
                Icon = "inventory_2",
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Cơ sở dữ liệu của PMC", Controller = "Materials", Action = "Index", Icon = "storage" },
                    new SideMenuItem { Title = "Quét lên kệ", Controller = "Inbound", Action = "Index", Icon = "qr_code_scanner" },
                    new SideMenuItem { Title = "Xuất hàng", Controller = "Issue", Action = "Index", Icon = "local_shipping" },
                    new SideMenuItem { Title = "Nhận lại hàng", Controller = "Return", Action = "Index", Icon = "assignment_return" },
                    new SideMenuItem { Title = "Hủy liệu", Controller = "Dispose", Action = "Index", Icon = "delete_forever" },
                }
            },
            new SideMenuItem
            {
                Id = "Reports",
                Title = "Báo cáo",
                Icon = "bar_chart",
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Đang chờ lên kệ", Controller = "Materials", Action = "Index", Icon = "hourglass_top", RouteValues = new { status = "Staging" } },
                    new SideMenuItem { Title = "Đang trong kho", Controller = "Materials", Action = "Index", Icon = "inventory", RouteValues = new { status = "InStock" } },
                    new SideMenuItem { Title = "Đã xuất 1 phần", Controller = "Materials", Action = "Index", Icon = "outbound", RouteValues = new { status = "PartiallyIssued" } },
                    new SideMenuItem { Title = "Đã xuất hết chưa nhận lại", Controller = "Materials", Action = "Index", Icon = "local_shipping", RouteValues = new { status = "IssuedOut" } },
                    new SideMenuItem { Title = "Quá 90 ngày chưa nhận lại", Controller = "Materials", Action = "Index", Icon = "warning", RouteValues = new { isOverdue = true } },
                    new SideMenuItem { Title = "Đã hủy", Controller = "Materials", Action = "Index", Icon = "delete_forever", RouteValues = new { status = "Disposed" } },
                }
            },
            new SideMenuItem
            {
                Id = "Admin",
                Title = "Quản trị",
                Icon = "admin_panel_settings",
                VisibleWhen = () => isAdmin,
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Người dùng", Controller = "Users", Action = "Index", Icon = "group" },
                    new SideMenuItem { Title = "Nơi nhận", Controller = "Recipients", Action = "Index", Icon = "add_location_alt" },
                }
            },
        };
    }
}
