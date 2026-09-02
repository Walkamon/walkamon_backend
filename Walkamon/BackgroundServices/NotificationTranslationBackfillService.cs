using BLL.Interfaces;
using DAL.Interfaces;

namespace Walkamon.BackgroundServices;

/// <summary>
/// Backfills the additive EN/VI columns introduced after older editorial
/// notifications had already been sent. Player read requests stay fast; the
/// worker translates a bounded batch and persists it once for all recipients.
/// </summary>
public sealed class NotificationTranslationBackfillService : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationTranslationBackfillService> _logger;

    public NotificationTranslationBackfillService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationTranslationBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = IdleInterval;
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == BatchSize)
                {
                    delay = TimeSpan.FromSeconds(1);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "Notification translation backfill batch failed.");
                delay = RetryInterval;
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<INotificationRepository>();
        var translator = scope.ServiceProvider
            .GetRequiredService<ITextTranslationService>();
        var notifications = await repository
            .GetNotificationsPendingTranslationAsync(BatchSize);
        if (notifications.Count == 0) return 0;

        var translated = 0;
        foreach (var notification in notifications)
        {
            stoppingToken.ThrowIfCancellationRequested();
            var title = await translator.TranslateAsync(
                notification.Title,
                stoppingToken);
            var body = await translator.TranslateAsync(
                notification.Body,
                stoppingToken);

            notification.TitleVi = title.Vietnamese;
            notification.TitleEn = title.English;
            notification.BodyVi = body.Vietnamese;
            notification.BodyEn = body.English;
            notification.SourceLanguageCode = title.SourceLanguageCode;
            notification.TranslationSourceHash = title.SourceHash;
            // Also timestamps a failed attempt so an unavailable sidecar does
            // not make the worker hot-loop over the same first page.
            notification.TranslatedAt =
                title.TranslatedAt ?? body.TranslatedAt ?? DateTime.UtcNow;
            notification.TranslationStatusCode =
                title.StatusCode == "translated" && body.StatusCode == "translated"
                    ? "translated"
                    : "fallback";
            notification.UpdatedAt = DateTime.UtcNow;
            repository.UpdateNotification(notification);
            if (notification.TranslationStatusCode == "translated") translated++;
        }

        await repository.SaveChangesAsync();
        _logger.LogInformation(
            "Notification translation backfill processed {ProcessedCount}; translated {TranslatedCount}.",
            notifications.Count,
            translated);
        return notifications.Count;
    }
}
