using System.Globalization;

namespace BLL.Service;

internal static class DailyActivityReminderConstants
{
    public const string NotificationTypeCode = "daily_step_goal_reminder";
    public const string EnabledSettingKey = "daily_activity_reminder_enabled";
    public const string DefaultGoalSettingKey = "daily_activity_reminder_default_goal";
    public const string LocalTimeSettingKey = "daily_activity_reminder_local_time";
    public const string GraceMinutesSettingKey = "daily_activity_reminder_grace_minutes";
    public const string FallbackTimeZoneId = "Asia/Ho_Chi_Minh";
    public const int FallbackDefaultGoal = 7000;
    public const int FallbackGraceMinutes = 120;
    public const int RetryLeaseMinutes = 5;
    public static readonly TimeOnly FallbackLocalTime = new(18, 0);
}

public enum DailyActivityReminderDecisionCode
{
    Eligible,
    AccountInactive,
    NotYetDue,
    WindowClosed,
    NotificationDisabled,
    MissingDeviceToken,
    GoalReached
}

internal sealed record DailyActivityReminderPolicyInput(
    DateTimeOffset UtcNow,
    string? TimeZoneId,
    bool AccountActive,
    bool NotificationsEnabled,
    bool HasActiveDeviceToken,
    int CurrentAuthoritativeSteps,
    int DailyGoal,
    TimeOnly ReminderLocalTime,
    int GraceMinutes);

internal sealed record DailyActivityReminderPolicyResult(
    DailyActivityReminderDecisionCode Decision,
    DateOnly LocalDate,
    DateTime LocalNow,
    int CurrentAuthoritativeSteps,
    int DailyGoal,
    int RemainingSteps,
    string EffectiveTimeZoneId,
    bool UsedFallbackTimeZone)
{
    public bool IsInsideReminderWindow =>
        Decision is not DailyActivityReminderDecisionCode.NotYetDue
            and not DailyActivityReminderDecisionCode.WindowClosed;
}

internal static class DailyActivityReminderPolicy
{
    public static DailyActivityReminderPolicyResult Evaluate(
        DailyActivityReminderPolicyInput input)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(input.CurrentAuthoritativeSteps);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.DailyGoal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.GraceMinutes);

        var (timeZone, usedFallback) = ResolveTimeZone(input.TimeZoneId);
        var utc = input.UtcNow.ToUniversalTime();
        var localNow = TimeZoneInfo.ConvertTime(utc, timeZone).DateTime;
        var localDate = DateOnly.FromDateTime(localNow);
        var windowStart = localDate.ToDateTime(input.ReminderLocalTime);
        var windowEnd = windowStart.AddMinutes(input.GraceMinutes);
        var remaining = Math.Max(
            input.DailyGoal - input.CurrentAuthoritativeSteps,
            0);

        var decision = !input.AccountActive
            ? DailyActivityReminderDecisionCode.AccountInactive
            : localNow < windowStart
                ? DailyActivityReminderDecisionCode.NotYetDue
                : localNow >= windowEnd
                    ? DailyActivityReminderDecisionCode.WindowClosed
                    : !input.NotificationsEnabled
                        ? DailyActivityReminderDecisionCode.NotificationDisabled
                        : input.CurrentAuthoritativeSteps >= input.DailyGoal
                            ? DailyActivityReminderDecisionCode.GoalReached
                            : !input.HasActiveDeviceToken
                                ? DailyActivityReminderDecisionCode.MissingDeviceToken
                                : DailyActivityReminderDecisionCode.Eligible;

        return new DailyActivityReminderPolicyResult(
            decision,
            localDate,
            localNow,
            input.CurrentAuthoritativeSteps,
            input.DailyGoal,
            remaining,
            timeZone.Id,
            usedFallback);
    }

    public static bool TryParseLocalTime(string? value, out TimeOnly localTime)
    {
        return TimeOnly.TryParseExact(
            value,
            ["HH:mm", "H:mm"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out localTime);
    }

    private static (TimeZoneInfo TimeZone, bool UsedFallback) ResolveTimeZone(
        string? requestedTimeZoneId)
    {
        if (TryFindTimeZone(requestedTimeZoneId, out var requested))
        {
            return (requested!, false);
        }

        if (TryFindTimeZone(
                DailyActivityReminderConstants.FallbackTimeZoneId,
                out var fallback))
        {
            return (fallback!, true);
        }

        if (TryFindTimeZone("SE Asia Standard Time", out fallback))
        {
            return (fallback!, true);
        }

        return (TimeZoneInfo.Utc, true);
    }

    private static bool TryFindTimeZone(
        string? timeZoneId,
        out TimeZoneInfo? timeZone)
    {
        timeZone = null;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
