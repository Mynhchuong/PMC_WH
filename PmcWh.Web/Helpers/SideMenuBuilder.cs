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
                TranslationKey = "menuMaterialsGroup",
                Icon = "inventory_2",
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Cơ sở dữ liệu của PMC", TranslationKey = "menuMaterialsDb", Controller = "Materials", Action = "Index", Icon = "storage" },
                    new SideMenuItem { Title = "Quét lên kệ", TranslationKey = "menuInbound", Controller = "Inbound", Action = "Index", Icon = "qr_code_scanner" },
                    new SideMenuItem { Title = "Xuất hàng", TranslationKey = "menuIssue", Controller = "Issue", Action = "Index", Icon = "local_shipping" },
                    new SideMenuItem { Title = "Hủy liệu", TranslationKey = "menuDispose", Controller = "Dispose", Action = "Index", Icon = "delete_forever", VisibleWhen = () => isAdmin },
                    new SideMenuItem { Title = "Hàng xuất quá 90 ngày", TranslationKey = "menuOverdue", Controller = "Overdue", Action = "Index", Icon = "warning_amber", VisibleWhen = () => isAdmin },
                    new SideMenuItem { Title = "Bản đồ kho 3D", TranslationKey = "menuWarehouseMap", Controller = "Warehouse", Action = "Map", Icon = "warehouse" },
                }
            },

            new SideMenuItem
            {
                Id = "Reports",
                Title = "Báo cáo",
                TranslationKey = "menuReportsGroup",
                Icon = "summarize",
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Báo cáo xuất kho", TranslationKey = "menuReportsIssues", Controller = "Reports", Action = "Issues", Icon = "receipt_long" },
                }
            },

            new SideMenuItem
            {
                Id = "Admin",
                Title = "Quản trị",
                TranslationKey = "menuAdminGroup",
                Icon = "admin_panel_settings",
                VisibleWhen = () => isAdmin,
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Người dùng", TranslationKey = "menuUsers", Controller = "Users", Action = "Index", Icon = "group" },
                    new SideMenuItem { Title = "Nơi nhận", TranslationKey = "menuRecipients", Controller = "Recipients", Action = "Index", Icon = "add_location_alt" },
                }
            },
        };
    }
}
