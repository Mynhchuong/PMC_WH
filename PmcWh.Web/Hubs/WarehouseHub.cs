using Microsoft.AspNetCore.SignalR;

namespace PmcWh.Web.Hubs;

/// <summary>Hub realtime cho bản đồ kho 3D. Khi có thao tác nhập/xuất/nhận lại/hủy làm đổi
/// tồn kho, server phát "warehouseChanged" → mọi client đang mở bản đồ tự tải lại trạng thái.</summary>
public class WarehouseHub : Hub
{
}
