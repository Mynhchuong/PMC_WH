using System.Text.Json;

namespace PmcWh.Web.Helpers;

/// <summary>
/// Đóng gói thông báo flash (TempData FlashSuccess/FlashError/FlashWarning) dạng key + tham số
/// thay vì câu tiếng Việt cứng — để trang JS (warehouse-i18n.js PmcWhI18n.renderFlash) tự tra từ
/// điển VI/EN rồi ráp câu lúc hiển thị. Nếu JS không parse được JSON (vd. message do Api trả về
/// thẳng, không qua helper này) thì tự fallback hiện nguyên văn — không cần đổi những chỗ đó.
/// </summary>
public static class FlashHelper
{
    /// <summary>1 thông báo đơn, key tra trong warehouse-i18n.js, args thay vào {0} {1} ... trong template.</summary>
    public static string Msg(string key, params string?[] args) =>
        JsonSerializer.Serialize(new { segments = new[] { new { key, args } } });

    /// <summary>Ghép nhiều đoạn (mỗi đoạn tự dịch riêng) thành 1 câu — dùng khi câu có nhiều phần tuỳ điều kiện.</summary>
    public static string Compose(params (string Key, string?[] Args)[] parts) =>
        JsonSerializer.Serialize(new { segments = parts.Select(p => new { key = p.Key, args = p.Args }) });
}
