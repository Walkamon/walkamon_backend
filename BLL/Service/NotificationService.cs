using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DalNotification = DAL.Models.Notification;

namespace BLL.Service;

public class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;
    private const int ScheduledBatchSize = 20;

    private static readonly string[] AllowedTargetAudienceCodes =
    [
        "all_users",
        "new_users",
        "level_10_plus",
        "inactive_7_days"
    ];

    private static readonly string[] AllowedStatusCodes =
    [
        "scheduled",
        "sent",
        "failed"
    ];

    private readonly INotificationRepository _notificationRepository;
    private readonly IFcmPushService _fcmPushService;
    private readonly ILogger<NotificationService> _logger;
    private readonly ITextTranslationService? _translationService;

    public NotificationService(
        INotificationRepository notificationRepository,
        IFcmPushService fcmPushService,
        ILogger<NotificationService> logger,
        ITextTranslationService? translationService = null)
    {
        _notificationRepository = notificationRepository;
        _fcmPushService = fcmPushService;
        _logger = logger;
        _translationService = translationService;
    }

    public async Task<NotificationSettingsResponse> UpdateSettingsAsync(
        Guid userId,
        NotificationSettingsRequest request)
    {
        var profile = await GetProfileOrThrowAsync(userId);
        var now = DateTime.UtcNow;

        profile.NotificationsEnabled = request.NotificationsEnabled;
        profile.UpdatedAt = now;

        _notificationRepository.UpdateUserProfile(profile);
        await _notificationRepository.SaveChangesAsync();

        return new NotificationSettingsResponse
        {
            NotificationsEnabled = profile.NotificationsEnabled,
            UpdatedAt = profile.UpdatedAt
        };
    }

    public async Task<NotificationListResponse> GetNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? typeCode,
        bool? isRead,
        string? acceptLanguage = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        typeCode = NormalizeOptionalTypeCode(typeCode);

        var (items, totalCount) =
            await _notificationRepository.GetUserNotificationsAsync(
                userId,
                page,
                pageSize,
                typeCode,
                isRead);
        var profile = await GetProfileOrThrowAsync(userId);
        var useEnglish = ResolveUseEnglish(acceptLanguage, profile.LanguageCode);

        return new NotificationListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Notifications = items.Select(item => ToListItemResponse(item, useEnglish)).ToList()
        };
    }

    public async Task<NotificationDetailResponse> GetNotificationDetailAsync(
        Guid userId,
        Guid notificationId,
        string? acceptLanguage = null)
    {
        var userNotification =
            await GetOwnedNotificationOrThrowAsync(userId, notificationId);

        if (userNotification.ReadAt == null)
        {
            userNotification.ReadAt = DateTime.UtcNow;
            _notificationRepository.UpdateUserNotification(userNotification);
            await _notificationRepository.SaveChangesAsync();
        }
        var profile = await GetProfileOrThrowAsync(userId);
        return ToDetailResponse(
            userNotification,
            ResolveUseEnglish(acceptLanguage, profile.LanguageCode));
    }

    public async Task DeleteNotificationAsync(Guid userId, Guid notificationId)
    {
        var userNotification =
            await GetOwnedNotificationOrThrowAsync(userId, notificationId);

        if (userNotification.DeletedAt == null)
        {
            userNotification.DeletedAt = DateTime.UtcNow;
            _notificationRepository.UpdateUserNotification(userNotification);
            await _notificationRepository.SaveChangesAsync();
        }
    }

    public async Task<DeviceTokenResponse> UpsertDeviceTokenAsync(
        Guid userId,
        DeviceTokenRequest request)
    {
        var token = request.FcmToken.Trim();
        var now = DateTime.UtcNow;
        var deviceToken =
            await _notificationRepository.GetDeviceTokenByTokenAsync(token);

        if (deviceToken == null)
        {
            deviceToken = new DeviceToken
            {
                UserId = userId,
                FcmToken = token,
                IsActive = true,
                UpdatedAt = now
            };

            await _notificationRepository.AddDeviceTokenAsync(deviceToken);
        }
        else
        {
            deviceToken.UserId = userId;
            deviceToken.IsActive = true;
            deviceToken.UpdatedAt = now;
            _notificationRepository.UpdateDeviceToken(deviceToken);
        }

        await _notificationRepository.SaveChangesAsync();

        return ToDeviceTokenResponse(deviceToken);
    }

    public async Task<DeviceTokenResponse> DeactivateDeviceTokenAsync(
        Guid userId,
        DeviceTokenRequest request)
    {
        var token = request.FcmToken.Trim();
        var deviceToken =
            await _notificationRepository.GetDeviceTokenByTokenAsync(token);

        if (deviceToken == null || deviceToken.UserId != userId)
        {
            throw new NotFoundException("Device token not found");
        }

        deviceToken.IsActive = false;
        deviceToken.UpdatedAt = DateTime.UtcNow;
        _notificationRepository.UpdateDeviceToken(deviceToken);
        await _notificationRepository.SaveChangesAsync();

        return ToDeviceTokenResponse(deviceToken);
    }

    public async Task CreateAndSendToUserAsync(
        Guid userId,
        string typeCode,
        string title,
        string body,
        string? imageUrl = null)
    {
        typeCode = NormalizeRequiredTypeCode(typeCode);
        var profile = await GetProfileOrThrowAsync(userId);
        var now = DateTime.UtcNow;

        var notification = new DalNotification
        {
            NotificationId = Guid.NewGuid(),
            NotificationTypeCode = typeCode,
            Title = title.Trim(),
            Body = body.Trim(),
            ContentCode = typeCode,
            ParamsJson = "{}",
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            TargetAudienceCode = "single_user",
            StatusCode = "sent",
            SentAt = now,
            RecipientCount = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        await ApplyTranslationAsync(notification, title, body);

        var userNotification = new UserNotification
        {
            UserId = userId,
            NotificationId = notification.NotificationId
        };

        await _notificationRepository.AddNotificationAsync(notification);
        await _notificationRepository.AddUserNotificationAsync(userNotification);
        await _notificationRepository.SaveChangesAsync();

        if (!profile.NotificationsEnabled)
        {
            return;
        }

        var deviceTokens =
            await _notificationRepository.GetActiveDeviceTokensForUserAsync(userId);

        foreach (var deviceToken in deviceTokens)
        {
            await SendAndHandleTokenAsync(deviceToken, notification);
        }
    }

    public async Task<AdminNotificationListResponse> GetAdminNotificationsAsync(
        int page,
        int pageSize,
        string? search,
        string? targetAudienceCode,
        string? statusCode,
        string? sortBy,
        string? sortDirection)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        targetAudienceCode = NormalizeOptionalTargetAudienceCode(targetAudienceCode);
        statusCode = NormalizeOptionalStatusCode(statusCode);
        sortBy = NormalizeSortBy(sortBy);
        var sortDescending = !string.Equals(
            sortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase);

        var (items, totalCount) =
            await _notificationRepository.GetAdminNotificationsAsync(
                page,
                pageSize,
                string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                targetAudienceCode,
                statusCode,
                sortBy,
                sortDescending);

        return new AdminNotificationListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Notifications = items.Select(ToAdminListItemResponse).ToList()
        };
    }

    public async Task<AdminNotificationDetailResponse> GetAdminNotificationDetailAsync(
        Guid notificationId)
    {
        var notification = await GetNotificationOrThrowAsync(notificationId);

        return ToAdminDetailResponse(notification);
    }

    public async Task<AdminNotificationDetailResponse> CreateAdminNotificationAsync(
        Guid adminUserId,
        CreateAdminNotificationRequest request)
    {
        var typeCode = NormalizeRequiredTypeCode(request.TypeCode);
        var targetAudienceCode =
            NormalizeRequiredTargetAudienceCode(request.TargetAudienceCode);
        var now = DateTime.UtcNow;
        var sendNow = request.SendNow || request.ScheduleTime == null;

        if (!sendNow && request.ScheduleTime <= now)
        {
            throw new BadRequestException("Schedule time must be in the future");
        }

        var notification = new DalNotification
        {
            NotificationId = Guid.NewGuid(),
            NotificationTypeCode = typeCode,
            Title = request.Title.Trim(),
            Body = request.Content.Trim(),
            ContentCode = typeCode,
            ParamsJson = "{}",
            TargetAudienceCode = targetAudienceCode,
            StatusCode = sendNow ? "sent" : "scheduled",
            ImageUrl = NormalizeOptionalString(request.ImageUrl),
            CreatedByUserId = adminUserId,
            ScheduledAt = sendNow ? null : request.ScheduleTime,
            CreatedAt = now,
            UpdatedAt = now
        };
        await ApplyTranslationAsync(notification, request.Title, request.Content);

        await _notificationRepository.AddNotificationAsync(notification);

        if (sendNow)
        {
            await DispatchNotificationAsync(notification, now);
        }

        await _notificationRepository.SaveChangesAsync();

        var savedNotification =
            await _notificationRepository.GetNotificationByIdAsync(notification.NotificationId)
            ?? notification;

        return ToAdminDetailResponse(savedNotification);
    }

    public async Task<AdminNotificationDetailResponse> UpdateAdminNotificationAsync(
        Guid notificationId,
        UpdateAdminNotificationRequest request)
    {
        var notification = await GetNotificationOrThrowAsync(notificationId);
        var now = DateTime.UtcNow;
        var isSent = notification.StatusCode == "sent";

        if (request.TypeCode != null)
        {
            notification.NotificationTypeCode =
                NormalizeRequiredTypeCode(request.TypeCode);
            // System notifications use the stable type/content code as their
            // localization key.  Keep it in sync when an admin edits type.
            notification.ContentCode = notification.NotificationTypeCode;
        }

        if (request.Title != null)
        {
            notification.Title = request.Title.Trim();
        }

        if (request.Content != null)
        {
            notification.Body = request.Content.Trim();
        }

        if (request.Title != null || request.Content != null)
        {
            await ApplyTranslationAsync(
                notification,
                request.Title ?? notification.Title,
                request.Content ?? notification.Body);
        }

        if (request.ImageUrl != null)
        {
            notification.ImageUrl = NormalizeOptionalString(request.ImageUrl);
        }

        if (request.TargetAudienceCode != null)
        {
            var targetAudienceCode =
                NormalizeRequiredTargetAudienceCode(request.TargetAudienceCode);

            if (isSent && targetAudienceCode != notification.TargetAudienceCode)
            {
                throw new BadRequestException(
                    "Cannot change target audience after notification has been sent");
            }

            notification.TargetAudienceCode = targetAudienceCode;
        }

        if (request.ScheduleTime.HasValue)
        {
            if (isSent)
            {
                throw new BadRequestException(
                    "Cannot change schedule time after notification has been sent");
            }

            if (request.ScheduleTime <= now)
            {
                throw new BadRequestException("Schedule time must be in the future");
            }

            notification.ScheduledAt = request.ScheduleTime;
            notification.StatusCode = "scheduled";
        }

        notification.UpdatedAt = now;

        _notificationRepository.UpdateNotification(notification);
        await _notificationRepository.SaveChangesAsync();

        return ToAdminDetailResponse(notification);
    }

    public async Task DeleteAdminNotificationAsync(Guid notificationId)
    {
        var notification = await GetNotificationOrThrowAsync(notificationId);

        await _notificationRepository.DeleteNotificationAsync(notification);
        await _notificationRepository.SaveChangesAsync();
    }

    public async Task<int> ProcessDueScheduledNotificationsAsync()
    {
        var now = DateTime.UtcNow;
        var notifications =
            await _notificationRepository.GetDueScheduledNotificationsAsync(
                now,
                ScheduledBatchSize);

        foreach (var notification in notifications)
        {
            await DispatchNotificationAsync(notification, now);
            _notificationRepository.UpdateNotification(notification);
        }

        if (notifications.Count > 0)
        {
            await _notificationRepository.SaveChangesAsync();
        }

        return notifications.Count;
    }

    private async Task DispatchNotificationAsync(
        DalNotification notification,
        DateTime now)
    {
        var recipientUserIds =
            await _notificationRepository.GetTargetAudienceUserIdsAsync(
                notification.TargetAudienceCode,
                now);

        notification.RecipientCount = recipientUserIds.Count;
        notification.SentAt = now;
        notification.StatusCode = "sent";
        notification.UpdatedAt = now;

        await _notificationRepository.AddUserNotificationsAsync(
            recipientUserIds.Select(userId => new UserNotification
            {
                UserId = userId,
                NotificationId = notification.NotificationId
            }));

        var deviceTokens = recipientUserIds.Count == 0
            ? []
            : await _notificationRepository.GetActiveDeviceTokensForUsersAsync(
                recipientUserIds);

        foreach (var deviceToken in deviceTokens)
        {
            var delivered = await SendAndHandleTokenAsync(deviceToken, notification);
            if (delivered)
            {
                notification.DeliverySuccessCount++;
            }
            else
            {
                notification.DeliveryFailureCount++;
            }
        }

        if (deviceTokens.Count > 0 && notification.DeliverySuccessCount == 0)
        {
            notification.StatusCode = "failed";
        }
    }

    private async Task<bool> SendAndHandleTokenAsync(
        DeviceToken deviceToken,
        DalNotification notification)
    {
        if (!_fcmPushService.IsConfigured)
        {
            _logger.LogWarning(
                "Firebase push skipped because Firebase credentials are not configured.");
            return false;
        }

        try
        {
            var profile = await _notificationRepository.GetUserProfileAsync(deviceToken.UserId);
            var useEnglish = profile?.LanguageCode?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
            var title = useEnglish ? notification.TitleEn : notification.TitleVi;
            var body = useEnglish ? notification.BodyEn : notification.BodyVi;
            await _fcmPushService.SendLocalizedAsync(
                deviceToken,
                notification,
                parameters: ParseParams(notification.ParamsJson),
                titleOverride: string.IsNullOrWhiteSpace(title) ? null : title,
                bodyOverride: string.IsNullOrWhiteSpace(body) ? null : body);
            return true;
        }
        catch (FirebaseMessagingException ex) when (IsInvalidToken(ex))
        {
            deviceToken.IsActive = false;
            deviceToken.UpdatedAt = DateTime.UtcNow;
            _notificationRepository.UpdateDeviceToken(deviceToken);
            await _notificationRepository.SaveChangesAsync();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send notification {NotificationId} to FCM token {DeviceTokenId}.",
                notification.NotificationId,
                deviceToken.DeviceTokenId);
            return false;
        }
    }

    private static bool IsInvalidToken(FirebaseMessagingException ex)
    {
        return ex.MessagingErrorCode is MessagingErrorCode.Unregistered
            or MessagingErrorCode.InvalidArgument;
    }

    private async Task<UserProfile> GetProfileOrThrowAsync(Guid userId)
    {
        var profile = await _notificationRepository.GetUserProfileAsync(userId);

        if (profile == null)
        {
            throw new NotFoundException("User profile not found");
        }

        return profile;
    }

    private async Task<UserNotification> GetOwnedNotificationOrThrowAsync(
        Guid userId,
        Guid notificationId)
    {
        var userNotification =
            await _notificationRepository.GetUserNotificationAsync(
                userId,
                notificationId);

        if (userNotification == null)
        {
            throw new NotFoundException("Notification not found");
        }

        return userNotification;
    }

    private async Task<DalNotification> GetNotificationOrThrowAsync(
        Guid notificationId)
    {
        var notification =
            await _notificationRepository.GetNotificationByIdAsync(notificationId);

        if (notification == null)
        {
            throw new NotFoundException("Notification not found");
        }

        return notification;
    }

    private static NotificationListItemResponse ToListItemResponse(
        UserNotification userNotification,
        bool useEnglish)
    {
        var notification = userNotification.Notification;

        return new NotificationListItemResponse
        {
            NotificationId = notification.NotificationId,
            Icon = NotificationTypeCatalog.GetIcon(notification.NotificationTypeCode),
            Title = Localized(notification.Title, notification.TitleVi, notification.TitleEn, useEnglish),
            ShortBody = ToShortBody(Localized(notification.Body, notification.BodyVi, notification.BodyEn, useEnglish)),
            CreatedAt = notification.CreatedAt,
            IsRead = userNotification.ReadAt != null,
            ReadAt = userNotification.ReadAt,
            TypeCode = notification.NotificationTypeCode
            ,ContentCode = notification.ContentCode
            ,Params = ParseParams(notification.ParamsJson)
        };
    }

    private static NotificationDetailResponse ToDetailResponse(
        UserNotification userNotification,
        bool useEnglish)
    {
        var notification = userNotification.Notification;

        return new NotificationDetailResponse
        {
            NotificationId = notification.NotificationId,
            Title = Localized(notification.Title, notification.TitleVi, notification.TitleEn, useEnglish),
            Body = Localized(notification.Body, notification.BodyVi, notification.BodyEn, useEnglish),
            CreatedAt = notification.CreatedAt,
            TypeCode = notification.NotificationTypeCode,
            Icon = NotificationTypeCatalog.GetIcon(notification.NotificationTypeCode),
            ImageUrl = notification.ImageUrl,
            IsRead = userNotification.ReadAt != null,
            ReadAt = userNotification.ReadAt
            ,ContentCode = notification.ContentCode
            ,Params = ParseParams(notification.ParamsJson)
        };
    }

    private static DeviceTokenResponse ToDeviceTokenResponse(DeviceToken token)
    {
        return new DeviceTokenResponse
        {
            DeviceTokenId = token.DeviceTokenId,
            FcmToken = token.FcmToken,
            IsActive = token.IsActive,
            UpdatedAt = token.UpdatedAt
        };
    }

    private static AdminNotificationListItemResponse ToAdminListItemResponse(
        DalNotification notification)
    {
        return new AdminNotificationListItemResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            TargetAudienceCode = notification.TargetAudienceCode,
            StatusCode = notification.StatusCode,
            SendTime = notification.SentAt ?? notification.ScheduledAt,
            CreatedBy = GetCreatedByDisplay(notification.CreatedByUser),
            CreatedAt = notification.CreatedAt
        };
    }

    private static AdminNotificationDetailResponse ToAdminDetailResponse(
        DalNotification notification)
    {
        return new AdminNotificationDetailResponse
        {
            NotificationId = notification.NotificationId,
            TypeCode = notification.NotificationTypeCode,
            Title = notification.Title,
            Content = notification.Body,
            TargetAudienceCode = notification.TargetAudienceCode,
            ScheduleTime = notification.ScheduledAt,
            SentAt = notification.SentAt,
            RecipientCount = notification.RecipientCount,
            StatusCode = notification.StatusCode,
            DeliverySuccessCount = notification.DeliverySuccessCount,
            DeliveryFailureCount = notification.DeliveryFailureCount,
            ImageUrl = notification.ImageUrl,
            CreatedBy = GetCreatedByDisplay(notification.CreatedByUser),
            CreatedAt = notification.CreatedAt,
            UpdatedAt = notification.UpdatedAt
            ,ContentCode = notification.ContentCode
            ,SourceLanguageCode = notification.SourceLanguageCode
            ,TitleVi = notification.TitleVi
            ,TitleEn = notification.TitleEn
            ,BodyVi = notification.BodyVi
            ,BodyEn = notification.BodyEn
            ,TranslationStatusCode = notification.TranslationStatusCode
        };
    }

    private async Task ApplyTranslationAsync(DalNotification notification, string title, string body)
    {
        var titlePair = _translationService == null
            ? new TranslatedTextPair("vi", title, title, title, "fallback", string.Empty, null)
            : await _translationService.TranslateAsync(title);
        var bodyPair = _translationService == null
            ? new TranslatedTextPair("vi", body, body, body, "fallback", string.Empty, null)
            : await _translationService.TranslateAsync(body);
        notification.TitleVi = titlePair.Vietnamese;
        notification.TitleEn = titlePair.English;
        notification.BodyVi = bodyPair.Vietnamese;
        notification.BodyEn = bodyPair.English;
        notification.SourceLanguageCode = titlePair.SourceLanguageCode;
        notification.TranslationStatusCode = titlePair.StatusCode == "translated" && bodyPair.StatusCode == "translated"
            ? "translated"
            : "fallback";
        notification.TranslationSourceHash = titlePair.SourceHash;
        notification.TranslatedAt = titlePair.TranslatedAt;
    }

    private static Dictionary<string, object?> ParseParams(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                ?? new Dictionary<string, object?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>();
        }
    }

    private static string Localized(string fallback, string? vi, string? en, bool useEnglish) =>
        (useEnglish ? en : vi)?.Trim() is { Length: > 0 } value ? value : fallback;

    private static bool ResolveUseEnglish(
        string? acceptLanguage,
        string? profileLanguageCode)
    {
        // The first supported language in Accept-Language wins.  Quality
        // weights do not change the order here because clients send their
        // selected locale first; unsupported entries are skipped.
        if (!string.IsNullOrWhiteSpace(acceptLanguage))
        {
            foreach (var preference in acceptLanguage.Split(','))
            {
                var language = preference.Split(';', 2)[0].Trim();
                if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (language.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return profileLanguageCode?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? GetCreatedByDisplay(User? user)
    {
        if (user == null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(user.UserProfile?.Username)
            ? user.Email
            : user.UserProfile.Username;
    }

    private static string ToShortBody(string body)
    {
        const int maxLength = 120;
        return body.Length <= maxLength
            ? body
            : $"{body[..maxLength]}...";
    }

    private static string? NormalizeOptionalTypeCode(string? typeCode)
    {
        if (string.IsNullOrWhiteSpace(typeCode))
        {
            return null;
        }

        return NormalizeRequiredTypeCode(typeCode);
    }

    private static string NormalizeRequiredTypeCode(string typeCode)
    {
        var normalized = typeCode.Trim().ToLowerInvariant();

        if (!NotificationTypeCatalog.IsValid(normalized))
        {
            throw new BadRequestException("Invalid notification type code");
        }

        return normalized;
    }

    private static string NormalizeRequiredTargetAudienceCode(
        string targetAudienceCode)
    {
        var normalized = targetAudienceCode.Trim().ToLowerInvariant();

        if (!AllowedTargetAudienceCodes.Contains(normalized))
        {
            throw new BadRequestException("Invalid target audience code");
        }

        return normalized;
    }

    private static string? NormalizeOptionalTargetAudienceCode(
        string? targetAudienceCode)
    {
        return string.IsNullOrWhiteSpace(targetAudienceCode)
            ? null
            : NormalizeRequiredTargetAudienceCode(targetAudienceCode);
    }

    private static string? NormalizeOptionalStatusCode(string? statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            return null;
        }

        var normalized = statusCode.Trim().ToLowerInvariant();
        if (!AllowedStatusCodes.Contains(normalized))
        {
            throw new BadRequestException("Invalid notification status code");
        }

        return normalized;
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return "created_at";
        }

        var normalized = sortBy.Trim().ToLowerInvariant();
        var allowed = new[]
        {
            "title",
            "target",
            "status",
            "send_time",
            "created_by",
            "created_at"
        };

        if (!allowed.Contains(normalized))
        {
            throw new BadRequestException("Invalid sort field");
        }

        return normalized;
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
