namespace PmcWh.Api.Services;

/// <summary>Chạy MaintenanceJobService 1 lần lúc Api khởi động, sau đó lặp lại mỗi 24 giờ.</summary>
public class MaintenanceBackgroundService : BackgroundService
{
    private readonly MaintenanceJobService _job;
    private readonly ILogger<MaintenanceBackgroundService> _logger;

    public MaintenanceBackgroundService(MaintenanceJobService job, ILogger<MaintenanceBackgroundService> logger)
    {
        _job = job;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                await _job.RunAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled maintenance job failed.");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
