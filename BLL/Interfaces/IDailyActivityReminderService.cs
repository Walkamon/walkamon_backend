namespace BLL.Interfaces;

public interface IDailyActivityReminderService
{
    Task<DailyActivityReminderRunResult> ProcessAsync(
        CancellationToken cancellationToken = default);
}

public sealed record DailyActivityReminderRunResult(
    DateTimeOffset ExecutedAtUtc,
    int EvaluatedUsers,
    int UsersInLocalWindow,
    int EligibleUsers,
    int SentUsers,
    int AlreadySentSkipped,
    int GoalReachedSkipped,
    int NotificationDisabledSkipped,
    int MissingTokenSkipped,
    int RetryDeferred,
    int Failures,
    int InvalidTimeZoneFallbacks,
    bool FeatureEnabled);
