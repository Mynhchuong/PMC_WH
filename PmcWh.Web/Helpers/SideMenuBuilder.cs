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
                Title = "Trang chủ",
                TranslationKey = "menuMaterialsGroup",
                Icon = "inventory_2",
                Children = new List<SideMenuItem>
                {
                    new SideMenuItem { Title = "Cơ sở dữ liệu", TranslationKey = "menuMaterialsDb", Controller = "Materials", Action = "Index", Icon = "storage", ColorFrom = "#0d6efd", ColorTo = "#0a58ca" },
                    new SideMenuItem { Title = "Quét lên kệ", TranslationKey = "menuInbound", Controller = "Inbound", Action = "Index", Icon = "qr_code_scanner", ColorFrom = "#198754", ColorTo = "#146c43" },
                    new SideMenuItem { Title = "Xuất hàng", TranslationKey = "menuIssue", Controller = "Issue", Action = "Index", Icon = "local_shipping", ColorFrom = "#fd7e14", ColorTo = "#c2570a" },
                    new SideMenuItem { Title = "Hủy liệu", TranslationKey = "menuDispose", Controller = "Dispose", Action = "Index", Icon = "delete_forever", ColorFrom = "#d63384", ColorTo = "#99245c", VisibleWhen = () => isAdmin },
                    new SideMenuItem { Title = "Hàng xuất quá 90 ngày", TranslationKey = "menuOverdue", Controller = "Overdue", Action = "Index", Icon = "warning_amber", ColorFrom = "#dc3545", ColorTo = "#842029", VisibleWhen = () => isAdmin },
                    new SideMenuItem { Title = "Bản đồ kho 3D", TranslationKey = "menuWarehouseMap", Controller = "Warehouse", Action = "Map", Icon = "warehouse", ColorFrom = "#ff8a3d", ColorTo = "#c9501a" },
                    new SideMenuItem { Title = "Thu thập Barcode", TranslationKey = "menuBarcodeCollection", Controller = "BarcodeCollection", Action = "Index", Icon = "qr_code_2", ColorFrom = "#20c997", ColorTo = "#12704f" },
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
                    new SideMenuItem { Title = "Báo cáo xuất kho", TranslationKey = "menuReportsIssues", Controller = "Reports", Action = "Issues", Icon = "receipt_long", ColorFrom = "#6f42c1", ColorTo = "#4b2e83" },
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
                    new SideMenuItem { Title = "Người dùng", TranslationKey = "menuUsers", Controller = "Users", Action = "Index", Icon = "group", ColorFrom = "#6610f2", ColorTo = "#4909ad" },
                    new SideMenuItem { Title = "Nơi nhận", TranslationKey = "menuRecipients", Controller = "Recipients", Action = "Index", Icon = "add_location_alt", ColorFrom = "#0dcaf0", ColorTo = "#087990" },
                    new SideMenuItem { Title = "Quản lý kệ", TranslationKey = "menuStorageLocations", Controller = "StorageLocations", Action = "Index", Icon = "shelves", ColorFrom = "#795548", ColorTo = "#4e342e" },
                }
            },
        };
    }
}
