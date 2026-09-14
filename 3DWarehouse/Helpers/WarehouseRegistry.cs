using Warehouse3D.Models;

namespace Warehouse3D.Helpers;

// Nơi DUY NHẤT khai báo danh sách kho trong toàn dự án — giống pattern SideMenuBuilder bên
// PmcWh.Web (khai báo 1 chỗ, mọi nơi khác chỉ đọc lại, không tự dựng danh sách riêng).
//
// THÊM 1 KHO MỚI — chỉ cần 2 bước, không phải sửa Controller/View/routing:
//   1. Thêm 1 entry vào danh sách Warehouses bên dưới (Id là slug dùng trong route + config,
//      vd "pmc", "abc" — chữ thường, không dấu, không khoảng trắng).
//   2. Set Oracle connection cho kho đó qua User Secrets (KHÔNG ghi thẳng vào appsettings.json —
//      xem CLAUDE.md gốc của PmcWh.Api, cùng lý do):
//         dotnet user-secrets set "Warehouses:{Id}:Oracle:Username" "..."
//         dotnet user-secrets set "Warehouses:{Id}:Oracle:Password" "..."
//         dotnet user-secrets set "Warehouses:{Id}:Oracle:DataSource" "..."
//      WarehouseController tự resolve đúng connection theo {warehouseId} trong route qua
//      OracleDataServiceFactory — không cần sửa Program.cs/DI.
//
// LƯU Ý — nếu kho mới không cùng schema với PMC (tên bảng PMC_StorageLocations/PMC_Materials/...
// khác đi, không chỉ khác connection): các câu SQL trong WarehouseController đang hard-code theo
// đúng schema PMC. Trường hợp đó phải tách WarehouseController thành 1 abstraction theo từng kho
// (vd IWarehouseQueryProvider implement riêng cho từng schema) — chưa làm trước vì hiện chưa có
// kho thứ 2 để biết schema thực tế khác PMC ở chỗ nào, tránh đoán mò dựng abstraction sai.
public static class WarehouseRegistry
{
    public static readonly IReadOnlyList<WarehouseDefinition> Warehouses = new List<WarehouseDefinition>
    {
        new()
        {
            Id = "pmc",
            Name = "KHO PMC",
            Description = "43 kệ — dữ liệu thật",
            Icon = "warehouse",
            IsActive = true,
            Schema = "pmc",
        },
        new()
        {
            Id = "plantc",
            Name = "PLANT C — NEWBALANCE MATERIAL W.H",
            Description = "35 kệ — dữ liệu thật",
            Icon = "factory",
            IsActive = true,
            Schema = "generic",
        },
        // new() { Id = "abc", Name = "KHO ABC", Description = "...", Icon = "warehouse", IsActive = true, Schema = "generic" },
    };

    public static WarehouseDefinition? Find(string id) =>
        Warehouses.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));
}
