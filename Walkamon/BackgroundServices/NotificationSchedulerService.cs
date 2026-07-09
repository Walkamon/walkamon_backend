using BLL.Interfaces;

namespace Walkamon.BackgroundServices;

public class NotificationSchedulerService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationSchedulerService> _logger;

    public NotificationSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled notification processing failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessDueNotificationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var notificationService =
            scope.ServiceProvider.GetRequiredService<INotificationService>();

        var processed =
            await notificationService.ProcessDueScheduledNotificationsAsync();

        if (processed > 0)
        {
            _logger.LogInformation(
                "Processed {NotificationCount} scheduled notifications.",
                processed);
        }
    }
}
