using HRSystem.BLL.Services;

namespace HRSystem.Web.Services;

// Marks absences / leaves / holidays for past days without a check-in.
// Runs at startup and every few hours over the last week, so a missed run or a leave approved
// later is picked up next time (CloseDaysAsync is safe to repeat).
public class AttendanceCloseJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private const int DaysBack = 7;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AttendanceCloseJob> _logger;

    public AttendanceCloseJob(IServiceScopeFactory scopeFactory, ILogger<AttendanceCloseJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceService>();

                var today = DateOnly.FromDateTime(DateTime.Now);
                var changed = await attendance.CloseDaysAsync(today.AddDays(-DaysBack), today.AddDays(-1));
                _logger.LogInformation("Attendance close: {Changed} records created or updated", changed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Attendance close failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
