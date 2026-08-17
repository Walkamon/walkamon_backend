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

            try
            {
                await ProcessDailyActivityRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily activity reminder processing failed.");
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

    private async Task ProcessDailyActivityRemindersAsync(
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var reminderService =
            scope.ServiceProvider.GetRequiredService<IDailyActivityReminderService>();
        var result = await reminderService.ProcessAsync(stoppingToken);

        _logger.LogInformation(
            "DAILY_ACTIVITY_REMINDER_JOB executedAtUtc={ExecutedAtUtc}, featureEnabled={FeatureEnabled}, evaluatedUsers={EvaluatedUsers}, usersInLocalWindow={UsersInLocalWindow}, eligibleUsers={EligibleUsers}, sentUsers={SentUsers}, alreadySentSkipped={AlreadySentSkipped}, goalReachedSkipped={GoalReachedSkipped}, notificationDisabledSkipped={NotificationDisabledSkipped}, missingTokenSkipped={MissingTokenSkipped}, retryDeferred={RetryDeferred}, failures={Failures}, invalidTimeZoneFallbacks={InvalidTimeZoneFallbacks}",
            result.ExecutedAtUtc,
            result.FeatureEnabled,
            result.EvaluatedUsers,
            result.UsersInLocalWindow,
            result.EligibleUsers,
            result.SentUsers,
            result.AlreadySentSkipped,
            result.GoalReachedSkipped,
            result.NotificationDisabledSkipped,
            result.MissingTokenSkipped,
            result.RetryDeferred,
            result.Failures,
            result.InvalidTimeZoneFallbacks);
    }
}
