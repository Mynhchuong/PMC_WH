using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Helpers;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class ReportsController : Controller
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;

    public ReportsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Issues(
        DateTime? fromDate, DateTime? toDate, int? recipientId, bool overdueOnly = false, int page = 1, int pageSize = 20)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");

        var query = BuildQuery("api/Reports/issues", fromDate, toDate, recipientId, overdueOnly) +
                    $"&page={page}&pageSize={pageSize}";

        var pagedTask = client.GetFromJsonAsync<PagedResultDto<IssueReportItem>>(query, ApiJsonOptions);
        var recipientsTask = client.GetFromJsonAsync<PagedResultDto<RecipientDto>>("api/Recipients?pageSize=200", ApiJsonOptions);
        await Task.WhenAll(pagedTask, recipientsTask);
        var paged = await pagedTask;
        var recipients = await recipientsTask;

        var model = new IssueReportListViewModel
        {
            Items = paged?.Items ?? new List<IssueReportItem>(),
            FromDate = fromDate,
            ToDate = toDate,
            RecipientId = recipientId,
            OverdueOnly = overdueOnly,
            Recipients = recipients?.Items ?? new List<RecipientDto>(),
            Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = paged?.TotalCount ?? 0,
                TotalPages = paged?.TotalPages ?? 0,
                Controller = "Reports",
                Action = "Issues",
                RouteValues = new Dictionary<string, string?>
                {
                    ["fromDate"] = fromDate?.ToString("yyyy-MM-dd"),
                    ["toDate"] = toDate?.ToString("yyyy-MM-dd"),
                    ["recipientId"] = recipientId?.ToString(),
                    ["overdueOnly"] = overdueOnly.ToString(),
                },
            },
        };

        return View(model);
    }

    public async Task<IActionResult> ExportIssuesExcel(DateTime? fromDate, DateTime? toDate, int? recipientId, bool overdueOnly = false)
    {
        var client = _httpClientFactory.CreateClient("PmcApi");
        var query = BuildQuery("api/Reports/issues/export", fromDate, toDate, recipientId, overdueOnly);

        var items = await client.GetFromJsonAsync<List<IssueReportItem>>(query, ApiJsonOptions) ?? new List<IssueReportItem>();

        var headers = new List<string>
        {
            "Barcode", "Dev", "Model", "Colorway", "Size", "Số lượng", "Đơn vị",
            "Xuất cho", "Người xuất", "Thời gian xuất", "Trạng thái", "Số ngày đã xuất", "Quá 90 ngày",
        };

        var rows = items.Select(i => (IReadOnlyList<object?>)new List<object?>
        {
            i.Barcode, i.Dev, i.Model, i.Colorway, i.SizeSpec, i.Qty, i.Unit,
            i.RecipientName, i.Username, i.OccurredAt.ToString("yyyy-MM-dd HH:mm"),
            i.Status, i.DaysOut, i.DaysOut > 90 ? "Có" : "",
        });

        var bytes = ExcelHelper.WriteRows(headers, rows);
        var fileName = $"BaoCaoXuatKho_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static string BuildQuery(string path, DateTime? fromDate, DateTime? toDate, int? recipientId, bool overdueOnly) =>
        $"{path}?fromDate={Uri.EscapeDataString(fromDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
        $"&toDate={Uri.EscapeDataString(toDate?.ToString("yyyy-MM-dd") ?? string.Empty)}" +
        $"&recipientId={Uri.EscapeDataString(recipientId?.ToString() ?? string.Empty)}" +
        $"&overdueOnly={overdueOnly}";

    private class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
