using ClosedXML.Excel;

namespace PmcWh.Web.Helpers;

public static class ExcelHelper
{
    // Số dòng xử lý mỗi lần import — hạn chế để tránh timeout/lock khi ghi
    // vào Oracle 10g. Đừng import nguyên file 1 lần, luôn chia theo ToBatches().
    public const int ImportBatchSize = 40;

    /// <summary>
    /// Đọc sheet đầu tiên của file Excel thành list dòng dữ liệu.
    /// Dòng đầu tiên được coi là header — key của mỗi dòng là tên cột trong header đó.
    /// Không ép kiểu, giá trị luôn là chuỗi (null nếu ô trống) — tự parse/convert khi build câu SQL.
    /// </summary>
    public static List<Dictionary<string, string?>> ReadRows(Stream fileStream)
    {
        var rows = new List<Dictionary<string, string?>>();

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.First();
        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
        {
            return rows;
        }

        var usedRows = usedRange.RowsUsed().ToList();
        if (usedRows.Count < 2)
        {
            return rows; // chỉ có header hoặc rỗng
        }

        var headers = usedRows[0].Cells().Select(c => c.GetString().Trim()).ToList();

        foreach (var dataRow in usedRows.Skip(1))
        {
            var row = new Dictionary<string, string?>();
            for (var col = 0; col < headers.Count; col++)
            {
                if (string.IsNullOrWhiteSpace(headers[col]))
                {
                    continue;
                }
                var cell = dataRow.Cell(col + 1);
                row[headers[col]] = cell.IsEmpty() ? null : GetCellText(cell);
            }
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Ô ngày tháng: GetString() trả về theo ĐÚNG định dạng hiển thị của ô trong file gốc (VD
    /// "21-Aug-26" nếu numFmt là d-mmm-yy), rất hay lệch khỏi các định dạng ngày mà MapRow ở
    /// MaterialsController chấp nhận (d/M/yyyy, yyyy-MM-dd) tuỳ file PMC gửi qua được format kiểu
    /// gì — khiến cả cột bị rớt âm thầm (không lỗi, không skip, chỉ ra null) như từng gặp ở cột ATA.
    /// Ép về "yyyy-MM-dd" cố định cho MỌI ô được Excel nhận diện là ngày, bất kể numFmt gốc.
    /// </summary>
    private static string GetCellText(IXLCell cell) =>
        cell.DataType == XLDataType.DateTime
            ? cell.GetDateTime().ToString("yyyy-MM-dd")
            : cell.GetString().Trim();

    /// <summary>
    /// Chia list dòng thành từng lô 40 dòng (mặc định) để import an toàn.
    /// Dùng: foreach (var batch in rows.ToBatches()) { ... insert batch vào Oracle ... }
    /// </summary>
    public static IEnumerable<List<Dictionary<string, string?>>> ToBatches(
        this List<Dictionary<string, string?>> rows, int batchSize = ImportBatchSize)
    {
        for (var i = 0; i < rows.Count; i += batchSize)
        {
            yield return rows.Skip(i).Take(batchSize).ToList();
        }
    }

    /// <summary>
    /// Xuất dữ liệu ra file Excel (.xlsx), trả về mảng byte để controller trả về File(...).
    /// </summary>
    public static byte[] WriteRows(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, string sheetName = "Sheet1")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        for (var col = 0; col < headers.Count; col++)
        {
            var headerCell = worksheet.Cell(1, col + 1);
            headerCell.Value = headers[col];
            headerCell.Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var col = 0; col < row.Count; col++)
            {
                SetCellValue(worksheet.Cell(rowIndex, col + 1), row[col]);
            }
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string s:
                cell.Value = s;
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case int or long or short or byte:
                cell.Value = Convert.ToInt64(value);
                break;
            case float or double or decimal:
                cell.Value = Convert.ToDouble(value);
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}
