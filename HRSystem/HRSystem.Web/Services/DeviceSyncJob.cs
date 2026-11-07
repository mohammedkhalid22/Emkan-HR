using HRSystem.BLL.Services;

namespace HRSystem.Web.Services;

// Pulls the devices this server can reach directly (same network) every few minutes.
// Devices behind the company firewall are sent by the agent instead.
public class DeviceSyncJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceSyncJob> _logger;

    public DeviceSyncJob(IServiceScopeFactory scopeFactory, ILogger<DeviceSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
                foreach (var id in await devices.PulledDeviceIdsAsync())
                {
                    // Each device in its own scope: one failing device doesn't hold back the rest
                    using var deviceScope = _scopeFactory.CreateScope();
                    await deviceScope.ServiceProvider.GetRequiredService<IDeviceService>().PullAsync(id, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Device sync failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
