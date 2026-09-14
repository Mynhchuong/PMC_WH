using Warehouse3D.Models;

namespace Warehouse3D.Services;

// Tạo OracleDataService cho đúng kho theo warehouseId — mỗi kho 1 connection riêng, đọc từ config
// section "Warehouses:{warehouseId}:Oracle" (giá trị thật nằm trong User Secrets, xem
// Helpers/WarehouseRegistry.cs để biết cách thêm kho mới). Đăng ký Singleton trong Program.cs —
// bản thân factory không giữ connection nào, chỉ đọc IConfiguration mỗi lần Create() được gọi.
public class OracleDataServiceFactory
{
    private readonly IConfiguration _configuration;

    public OracleDataServiceFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public OracleDataService Create(string warehouseId)
    {
        var section = _configuration.GetSection($"Warehouses:{warehouseId}:Oracle");
        var options = section.Get<OracleConnectionOptions>() ?? new OracleConnectionOptions();
        return new OracleDataService(options);
    }
}
