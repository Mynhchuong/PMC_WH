namespace PmcWh.Web.Helpers;

public static class PaginationHelper
{
    /// <summary>Số dòng/trang cho người dùng chọn. 0 nghĩa là "Tất cả" (không phân trang).</summary>
    public static readonly int[] PageSizeOptions = { 10, 20, 50, 100 };

    /// <summary>
    /// Tính danh sách số trang hiển thị quanh trang hiện tại (không hiện hết
    /// khi có hàng trăm trang). Giá trị 0 trong kết quả nghĩa là dấu "...".
    /// Ví dụ trang 6/20, window=2 -> 1, 0, 4, 5, 6, 7, 8, 0, 20
    /// </summary>
    public static List<int> GetPageWindow(int currentPage, int totalPages, int window = 2)
    {
        var pages = new List<int>();
        if (totalPages < 1)
        {
            return pages;
        }

        var start = Math.Max(1, currentPage - window);
        var end = Math.Min(totalPages, currentPage + window);

        if (start > 1)
        {
            pages.Add(1);
            if (start > 2)
            {
                pages.Add(0);
            }
        }

        for (var p = start; p <= end; p++)
        {
            pages.Add(p);
        }

        if (end < totalPages)
        {
            if (end < totalPages - 1)
            {
                pages.Add(0);
            }
            pages.Add(totalPages);
        }

        return pages;
    }
}
