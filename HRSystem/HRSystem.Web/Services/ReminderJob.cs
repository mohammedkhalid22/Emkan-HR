using HRSystem.BLL.Services;

namespace HRSystem.Web.Services;

// Daily reminders: contracts and documents about to end, birthdays, work anniversaries.
// Runs shortly after startup and every few hours; each reminder carries a key, so repeats send nothing new.
public class ReminderJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(IServiceScopeFactory scopeFactory, ILogger<ReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and the database migrate) first
        await Task.Delay(StartDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var sent = await notifications.SendDailyRemindersAsync(DateOnly.FromDateTime(DateTime.Now));
                _logger.LogInformation("Reminders: {Sent} sent", sent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reminders failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
