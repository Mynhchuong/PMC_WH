using PmcWh.Web.Models;

namespace PmcWh.Web.Helpers;

public static class SideMenuBuilder
{
    public static List<SideMenuItem> Build()
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
        };
    }
}
