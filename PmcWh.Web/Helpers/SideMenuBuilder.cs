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
