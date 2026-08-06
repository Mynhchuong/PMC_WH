namespace PmcWh.Api.Services;

public record MaintenanceJobResult(int OverdueFlagged, int DisposedArchived);

/// <summary>
/// 2 job bảo trì hằng ngày theo master plan mục 6:
/// - Đánh cờ IsOverdue cho liệu out quá 90 ngày chưa return (không tự set 0, Admin mới được xoá).
/// - Ẩn (IsArchived) liệu đã hủy quá 30 ngày.
/// </summary>
public class MaintenanceJobService
{
    private readonly OracleDataService _db;
    private readonly ILogger<MaintenanceJobService> _logger;

    public MaintenanceJobService(OracleDataService db, ILogger<MaintenanceJobService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MaintenanceJobResult> RunAsync()
    {
        var overdueFlagged = await _db.ExecuteAsync(
            @"UPDATE PMC_Materials
                 SET IsOverdue = 1
               WHERE Status IN ('IssuedOut','PartiallyIssued')
                 AND IsArchived = 0
                 AND IsOverdue = 0
                 AND LastIssuedAt < SYSTIMESTAMP - INTERVAL '90' DAY");

        var disposedArchived = await _db.ExecuteAsync(
            @"UPDATE PMC_Materials
                 SET IsArchived = 1
               WHERE Status = 'Disposed'
                 AND IsArchived = 0
                 AND DisposedAt < SYSTIMESTAMP - INTERVAL '30' DAY");

        _logger.LogInformation(
            "Maintenance job: flagged {OverdueFlagged} overdue, archived {DisposedArchived} disposed.",
            overdueFlagged, disposedArchived);

        return new MaintenanceJobResult(overdueFlagged, disposedArchived);
    }
}
