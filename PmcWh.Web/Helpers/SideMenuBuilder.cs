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
                Id = "Overview",
                Title = "Tổng quan",
                Icon = "dashboard",
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Dashboard", Controller = "Home", Action = "Index",   Icon = "space_dashboard" },
                    new SideMenuItem { Title = "Privacy",   Controller = "Home", Action = "Privacy", Icon = "info"            },
                }
            },
            new SideMenuItem
            {
                Id = "Materials",
                Title = "Liệu",
                Icon = "inventory_2",
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Import (Staging)", Controller = "Materials", Action = "Import", Icon = "upload_file" },
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
                }
            },
        };
    }
}
