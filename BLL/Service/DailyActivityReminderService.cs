using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BLL.Interfaces;
using DAL.Data;
using DAL.Extensions;
using DAL.Models;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DalNotification = DAL.Models.Notification;

namespace BLL.Service;

public sealed class DailyActivityReminderService : IDailyActivityReminderService
{
    private static readonly string[] SettingKeys =
    [
        DailyActivityReminderConstants.EnabledSettingKey,
        DailyActivityReminderConstants.DefaultGoalSettingKey,
        DailyActivityReminderConstants.LocalTimeSettingKey,
        DailyActivityReminderConstants.GraceMinutesSettingKey
    ];

    private readonly WalkamonContext _context;
    private readonly IFcmPushService _fcmPushService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DailyActivityReminderService> _logger;

    public DailyActivityReminderService(
        WalkamonContext context,
        IFcmPushService fcmPushService,
        TimeProvider timeProvider,
        ILogger<DailyActivityReminderService> logger)
    {
        _context = context;
        _fcmPushService = fcmPushService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DailyActivityReminderRunResult> ProcessAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().ToUniversalTime();
        var settings = await LoadSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            return new DailyActivityReminderRunResult(
                now, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.DeletedAt == null &&
                x.StatusCode == "active" &&
                x.Role.RoleName == "User")
            .Select(x => new ReminderUser(
                x.UserId,
                x.UserProfile != null && x.UserProfile.NotificationsEnabled,
                x.UserProfile == null ? null : x.UserProfile.LanguageCode,
                x.UserProfile == null ? null : x.UserProfile.TimeZoneId,
                x.DeviceTokens.Any(token => token.IsActive)))
            .ToListAsync(cancellationToken);

        var metrics = new RunMetrics { EvaluatedUsers = users.Count };
        var usersInWindow = new List<ReminderUserLocalContext>();

        foreach (var user in users.OrderBy(x => x.UserId))
        {
            var schedule = DailyActivityReminderPolicy.Evaluate(new(
                now,
                user.TimeZoneId,
                AccountActive: true,
                user.NotificationsEnabled,
                user.HasActiveDeviceToken,
                CurrentAuthoritativeSteps: 0,
                DailyGoal: int.MaxValue,
                settings.LocalTime,
                settings.GraceMinutes));

            if (schedule.UsedFallbackTimeZone)
            {
                metrics.InvalidTimeZoneFallbacks++;
                _logger.LogWarning(
                    "Daily activity reminder used fallback timezone for user {UserId}; configuredTimeZone={ConfiguredTimeZone}, effectiveTimeZone={EffectiveTimeZone}.",
                    user.UserId,
                    string.IsNullOrWhiteSpace(user.TimeZoneId) ? "missing" : "invalid",
                    schedule.EffectiveTimeZoneId);
            }

            if (!schedule.IsInsideReminderWindow)
            {
                continue;
            }

            metrics.UsersInLocalWindow++;
            usersInWindow.Add(new ReminderUserLocalContext(
                user,
                schedule.LocalDate,
                schedule.EffectiveTimeZoneId));
        }

        if (usersInWindow.Count == 0)
        {
            return metrics.ToResult(now, true);
        }

        var evidence = await LoadAuthoritativeEvidenceAsync(
            usersInWindow,
            cancellationToken);

        foreach (var localContext in usersInWindow
                     .OrderBy(x => x.LocalDate)
                     .ThenBy(x => x.User.UserId))
        {
            var key = (localContext.User.UserId, localContext.LocalDate);
            var currentSteps = evidence.AuthoritativeSteps.GetValueOrDefault(key, 0);
            var dailyGoal = evidence.CustomGoals.GetValueOrDefault(
                key,
                settings.DefaultGoal);
            var decision = DailyActivityReminderPolicy.Evaluate(new(
                now,
                localContext.EffectiveTimeZoneId,
                AccountActive: true,
                localContext.User.NotificationsEnabled,
                localContext.User.HasActiveDeviceToken,
                currentSteps,
                dailyGoal,
                settings.LocalTime,
                settings.GraceMinutes));

            switch (decision.Decision)
            {
                case DailyActivityReminderDecisionCode.NotificationDisabled:
                    metrics.NotificationDisabledSkipped++;
                    continue;
                case DailyActivityReminderDecisionCode.GoalReached:
                    metrics.GoalReachedSkipped++;
                    continue;
                case DailyActivityReminderDecisionCode.MissingDeviceToken:
                    metrics.MissingTokenSkipped++;
                    continue;
                case DailyActivityReminderDecisionCode.Eligible:
                    metrics.EligibleUsers++;
                    break;
                default:
                    continue;
            }

            var content = CreateLocalizedContent(
                localContext.User.LanguageCode,
                decision.CurrentAuthoritativeSteps,
                decision.RemainingSteps,
                decision.DailyGoal);
            var claim = await TryClaimAsync(
                localContext.User.UserId,
                localContext.LocalDate,
                content,
                now,
                cancellationToken);

            if (claim.Status == ReminderClaimStatus.AlreadySent)
            {
                metrics.AlreadySentSkipped++;
                continue;
            }

            if (claim.Status == ReminderClaimStatus.RetryDeferred)
            {
                metrics.RetryDeferred++;
                continue;
            }

            if (claim.Notification == null)
            {
                metrics.Failures++;
                continue;
            }

            var delivery = await DeliverAsync(
                localContext.User.UserId,
                claim.Notification,
                now,
                cancellationToken,
                new Dictionary<string, object?>
                {
                    ["currentSteps"] = decision.CurrentAuthoritativeSteps,
                    ["remainingSteps"] = decision.RemainingSteps,
                    ["dailyGoal"] = decision.DailyGoal
                });

            if (delivery.MissingDestination)
            {
                metrics.MissingTokenSkipped++;
            }
            else if (delivery.Delivered)
            {
                metrics.SentUsers++;
            }
            else
            {
                metrics.Failures++;
            }
        }

        return metrics.ToResult(now, true);
    }

    private async Task<ReminderSettings> LoadSettingsAsync(
        CancellationToken cancellationToken)
    {
        var values = await _context.SystemSettings
            .AsNoTracking()
            .Where(x => SettingKeys.Contains(x.SettingKey))
            .ToDictionaryAsync(
                x => x.SettingKey,
                x => x.SettingValue,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        var enabled = values.TryGetValue(
                DailyActivityReminderConstants.EnabledSettingKey,
                out var enabledValue) &&
            bool.TryParse(enabledValue, out var parsedEnabled) &&
            parsedEnabled;
        var defaultGoal = values.TryGetValue(
                DailyActivityReminderConstants.DefaultGoalSettingKey,
                out var goalValue) &&
            int.TryParse(goalValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedGoal) &&
            parsedGoal > 0
                ? parsedGoal
                : DailyActivityReminderConstants.FallbackDefaultGoal;
        var localTime = values.TryGetValue(
                DailyActivityReminderConstants.LocalTimeSettingKey,
                out var timeValue) &&
            DailyActivityReminderPolicy.TryParseLocalTime(timeValue, out var parsedTime)
                ? parsedTime
                : DailyActivityReminderConstants.FallbackLocalTime;
        var graceMinutes = values.TryGetValue(
                DailyActivityReminderConstants.GraceMinutesSettingKey,
                out var graceValue) &&
            int.TryParse(graceValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedGrace) &&
            parsedGrace > 0
                ? parsedGrace
                : DailyActivityReminderConstants.FallbackGraceMinutes;

        return new ReminderSettings(
            enabled,
            defaultGoal,
            localTime,
            graceMinutes);
    }

    private async Task<ReminderEvidence> LoadAuthoritativeEvidenceAsync(
        IReadOnlyCollection<ReminderUserLocalContext> users,
        CancellationToken cancellationToken)
    {
        var authoritativeSteps = new Dictionary<(Guid UserId, DateOnly Date), int>();
        var customGoals = new Dictionary<(Guid UserId, DateOnly Date), int>();

        foreach (var dateGroup in users.GroupBy(x => x.LocalDate))
        {
            var date = dateGroup.Key;
            foreach (var userIdChunk in dateGroup
                         .Select(x => x.User.UserId)
                         .Distinct()
                         .Chunk(1000))
            {
                var dailyRows = await _context.DailySteps
                    .AsNoTracking()
                    .Where(x => userIdChunk.Contains(x.UserId) && x.StepDate == date)
                    .Select(x => new { x.UserId, x.EligibleStepCount })
                    .ToListAsync(cancellationToken);
                foreach (var row in dailyRows)
                {
                    authoritativeSteps[(row.UserId, date)] =
                        Math.Max(0, row.EligibleStepCount);
                }

                var goalRows = await _context.StepGoals
                    .AsNoTracking()
                    .Where(x => userIdChunk.Contains(x.UserId) && x.EffectiveFrom <= date)
                    .OrderBy(x => x.UserId)
                    .ThenByDescending(x => x.EffectiveFrom)
                    .ToListAsync(cancellationToken);
                foreach (var goalGroup in goalRows.GroupBy(x => x.UserId))
                {
                    var goal = goalGroup.First();
                    if (goal.TargetSteps > 0)
                    {
                        customGoals[(goal.UserId, date)] = goal.TargetSteps;
                    }
                }
            }
        }

        return new ReminderEvidence(authoritativeSteps, customGoals);
    }

    private async Task<ReminderClaim> TryClaimAsync(
        Guid userId,
        DateOnly localDate,
        ReminderContent content,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var notificationId = CreateNotificationId(userId, localDate);
        var lockResource = $"walkamon:daily-step-reminder:{notificationId:N}";
        var nowUtc = now.UtcDateTime;
        var retryCutoff = nowUtc.AddMinutes(
            -DailyActivityReminderConstants.RetryLeaseMinutes);

        return await _context.ExecuteInTransactionAsync(
            IsolationLevel.ReadCommitted,
            async () =>
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($$"""
                    DECLARE @lock_result INT;
                    EXEC @lock_result = sys.sp_getapplock
                        @Resource = {{lockResource}},
                        @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction',
                        @LockTimeout = 5000;
                    IF @lock_result < 0
                        THROW 51020, 'Unable to acquire daily activity reminder lock.', 1;
                    """, cancellationToken);

                var notification = await _context.Notifications
                    .SingleOrDefaultAsync(
                        x => x.NotificationId == notificationId,
                        cancellationToken);

                if (notification == null)
                {
                    notification = new DalNotification
                    {
                        NotificationId = notificationId,
                        NotificationTypeCode = DailyActivityReminderConstants.NotificationTypeCode,
                        Title = content.Title,
                        Body = content.Body,
                        TargetAudienceCode = "single_user",
                        StatusCode = "scheduled",
                        ScheduledAt = nowUtc,
                        RecipientCount = 1,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };
                    _context.Notifications.Add(notification);
                    _context.UserNotifications.Add(new UserNotification
                    {
                        UserId = userId,
                        NotificationId = notificationId
                    });
                    await _context.SaveChangesAsync(cancellationToken);
                    return new ReminderClaim(ReminderClaimStatus.Claimed, notification);
                }

                if (notification.StatusCode == "sent")
                {
                    return new ReminderClaim(ReminderClaimStatus.AlreadySent, null);
                }

                if (notification.UpdatedAt > retryCutoff)
                {
                    return new ReminderClaim(ReminderClaimStatus.RetryDeferred, null);
                }

                notification.Title = content.Title;
                notification.Body = content.Body;
                notification.StatusCode = "scheduled";
                notification.ScheduledAt = nowUtc;
                notification.UpdatedAt = nowUtc;
                await _context.SaveChangesAsync(cancellationToken);
                return new ReminderClaim(ReminderClaimStatus.Claimed, notification);
            });
    }

    private async Task<ReminderDelivery> DeliverAsync(
        Guid userId,
        DalNotification notification,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var tokens = await _context.DeviceTokens
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderBy(x => x.DeviceTokenId)
            .ToListAsync(cancellationToken);
        if (tokens.Count == 0)
        {
            await CompleteDeliveryAsync(notification, 0, 0, now, cancellationToken);
            return new ReminderDelivery(false, true);
        }

        var successes = 0;
        var failures = 0;
        foreach (var token in tokens)
        {
            if (!_fcmPushService.IsConfigured)
            {
                failures++;
                continue;
            }

            try
            {
                await _fcmPushService.SendAsync(
                    token,
                    notification,
                    cancellationToken,
                    parameters);
                successes++;
            }
            catch (FirebaseMessagingException ex) when (IsInvalidToken(ex))
            {
                token.IsActive = false;
                token.UpdatedAt = now.UtcDateTime;
                failures++;
            }
            catch (Exception ex)
            {
                failures++;
                _logger.LogWarning(
                    ex,
                    "Daily activity reminder FCM delivery failed for notification {NotificationId}, deviceTokenId={DeviceTokenId}.",
                    notification.NotificationId,
                    token.DeviceTokenId);
            }
        }

        await CompleteDeliveryAsync(
            notification,
            successes,
            failures,
            now,
            cancellationToken);
        return new ReminderDelivery(successes > 0, false);
    }

    private async Task CompleteDeliveryAsync(
        DalNotification notification,
        int successes,
        int failures,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        notification.DeliverySuccessCount = checked(
            notification.DeliverySuccessCount + successes);
        notification.DeliveryFailureCount = checked(
            notification.DeliveryFailureCount + failures);
        notification.StatusCode = successes > 0 ? "sent" : "failed";
        notification.SentAt = successes > 0 ? now.UtcDateTime : null;
        notification.UpdatedAt = now.UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsInvalidToken(FirebaseMessagingException ex) =>
        ex.MessagingErrorCode is MessagingErrorCode.Unregistered
            or MessagingErrorCode.InvalidArgument;

    private static Guid CreateNotificationId(Guid userId, DateOnly localDate)
    {
        var identity = string.Create(
            CultureInfo.InvariantCulture,
            $"{DailyActivityReminderConstants.NotificationTypeCode}|{userId:D}|{localDate:yyyy-MM-dd}");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Guid.ParseExact(
            $"{hash[..8]}-{hash[8..12]}-{hash[12..16]}-{hash[16..20]}-{hash[20..32]}",
            "D");
    }

    private static ReminderContent CreateLocalizedContent(
        string? languageCode,
        int currentSteps,
        int remainingSteps,
        int dailyGoal)
    {
        var isVietnamese = languageCode?.StartsWith(
            "vi",
            StringComparison.OrdinalIgnoreCase) == true;
        var culture = CultureInfo.GetCultureInfo(isVietnamese ? "vi-VN" : "en-US");
        var current = currentSteps.ToString("N0", culture);
        var remaining = remainingSteps.ToString("N0", culture);
        var goal = dailyGoal.ToString("N0", culture);

        return isVietnamese
            ? new ReminderContent(
                "Cùng đi thêm một chút nhé! 🌱",
                $"Hôm nay bạn đã đi {current} bước. Còn {remaining} bước nữa để đạt mục tiêu {goal} bước.")
            : new ReminderContent(
                "A few more steps today! 🌱",
                $"You've walked {current} steps today. Only {remaining} more to reach your {goal}-step goal.");
    }

    private sealed record ReminderSettings(
        bool Enabled,
        int DefaultGoal,
        TimeOnly LocalTime,
        int GraceMinutes);

    private sealed record ReminderUser(
        Guid UserId,
        bool NotificationsEnabled,
        string? LanguageCode,
        string? TimeZoneId,
        bool HasActiveDeviceToken);

    private sealed record ReminderUserLocalContext(
        ReminderUser User,
        DateOnly LocalDate,
        string EffectiveTimeZoneId);

    private sealed record ReminderEvidence(
        Dictionary<(Guid UserId, DateOnly Date), int> AuthoritativeSteps,
        Dictionary<(Guid UserId, DateOnly Date), int> CustomGoals);

    private sealed record ReminderContent(string Title, string Body);

    private enum ReminderClaimStatus
    {
        Claimed,
        AlreadySent,
        RetryDeferred
    }

    private sealed record ReminderClaim(
        ReminderClaimStatus Status,
        DalNotification? Notification);

    private sealed record ReminderDelivery(bool Delivered, bool MissingDestination);

    private sealed class RunMetrics
    {
        public int EvaluatedUsers { get; set; }
        public int UsersInLocalWindow { get; set; }
        public int EligibleUsers { get; set; }
        public int SentUsers { get; set; }
        public int AlreadySentSkipped { get; set; }
        public int GoalReachedSkipped { get; set; }
        public int NotificationDisabledSkipped { get; set; }
        public int MissingTokenSkipped { get; set; }
        public int RetryDeferred { get; set; }
        public int Failures { get; set; }
        public int InvalidTimeZoneFallbacks { get; set; }

        public DailyActivityReminderRunResult ToResult(
            DateTimeOffset executedAtUtc,
            bool featureEnabled) => new(
                executedAtUtc,
                EvaluatedUsers,
                UsersInLocalWindow,
                EligibleUsers,
                SentUsers,
                AlreadySentSkipped,
                GoalReachedSkipped,
                NotificationDisabledSkipped,
                MissingTokenSkipped,
                RetryDeferred,
                Failures,
                InvalidTimeZoneFallbacks,
                featureEnabled);
    }
}
