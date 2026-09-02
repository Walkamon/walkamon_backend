using DAL.Data;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository;

public class NotificationRepository : INotificationRepository
{
    private readonly WalkamonContext _context;

    public NotificationRepository(WalkamonContext context)
    {
        _context = context;
    }

    public Task<UserProfile?> GetUserProfileAsync(Guid userId)
    {
        return _context.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<(List<UserNotification> Items, int TotalCount)> GetUserNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? typeCode,
        bool? isRead)
    {
        var query = _context.UserNotifications
            .AsNoTracking()
            .Include(x => x.Notification)
            .Where(x => x.UserId == userId && x.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(typeCode))
        {
            query = query.Where(x =>
                x.Notification.NotificationTypeCode == typeCode);
        }

        if (isRead.HasValue)
        {
            query = isRead.Value
                ? query.Where(x => x.ReadAt != null)
                : query.Where(x => x.ReadAt == null);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.Notification.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<UserNotification?> GetUserNotificationAsync(
        Guid userId,
        Guid notificationId)
    {
        return _context.UserNotifications
            .Include(x => x.Notification)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId
                && x.NotificationId == notificationId
                && x.DeletedAt == null);
    }

    public async Task<(List<Notification> Items, int TotalCount)> GetAdminNotificationsAsync(
        int page,
        int pageSize,
        string? search,
        string? targetAudienceCode,
        string? statusCode,
        string? sortBy,
        bool sortDescending)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
                .ThenInclude(x => x!.UserProfile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Title.Contains(search)
                || x.Body.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(targetAudienceCode))
        {
            query = query.Where(x =>
                x.TargetAudienceCode == targetAudienceCode);
        }

        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            query = query.Where(x => x.StatusCode == statusCode);
        }

        query = (sortBy, sortDescending) switch
        {
            ("title", false) => query.OrderBy(x => x.Title),
            ("title", true) => query.OrderByDescending(x => x.Title),
            ("target", false) => query.OrderBy(x => x.TargetAudienceCode),
            ("target", true) => query.OrderByDescending(x => x.TargetAudienceCode),
            ("status", false) => query.OrderBy(x => x.StatusCode),
            ("status", true) => query.OrderByDescending(x => x.StatusCode),
            ("send_time", false) => query.OrderBy(x => x.SentAt ?? x.ScheduledAt ?? x.CreatedAt),
            ("send_time", true) => query.OrderByDescending(x => x.SentAt ?? x.ScheduledAt ?? x.CreatedAt),
            ("created_by", false) => query.OrderBy(x => x.CreatedByUser!.Email),
            ("created_by", true) => query.OrderByDescending(x => x.CreatedByUser!.Email),
            ("created_at", false) => query.OrderBy(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<Notification?> GetNotificationByIdAsync(Guid notificationId)
    {
        return _context.Notifications
            .Include(x => x.CreatedByUser)
                .ThenInclude(x => x!.UserProfile)
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId);
    }

    public Task<List<Guid>> GetTargetAudienceUserIdsAsync(
        string targetAudienceCode,
        DateTime now)
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.UserPet)
            .Where(x =>
                x.DeletedAt == null
                && x.StatusCode == "active"
                && x.Role.RoleName == "User");

        query = targetAudienceCode switch
        {
            "new_users" => query.Where(x => x.CreatedAt >= now.AddDays(-7)),
            "level_10_plus" => query.Where(x => x.UserPet != null && x.UserPet.Level >= 10),
            "inactive_7_days" => query.Where(x =>
                x.LastLoginAt == null || x.LastLoginAt <= now.AddDays(-7)),
            _ => query
        };

        return query
            .Select(x => x.UserId)
            .ToListAsync();
    }

    public Task<List<DeviceToken>> GetActiveDeviceTokensForUsersAsync(
        IReadOnlyCollection<Guid> userIds)
    {
        return _context.DeviceTokens
            .Where(x => userIds.Contains(x.UserId) && x.IsActive)
            .ToListAsync();
    }

    public Task<List<Notification>> GetDueScheduledNotificationsAsync(
        DateTime now,
        int take)
    {
        return _context.Notifications
            .Where(x =>
                x.StatusCode == "scheduled"
                && x.TargetAudienceCode != "single_user"
                && x.ScheduledAt != null
                && x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt)
            .Take(take)
            .ToListAsync();
    }

    public Task<List<Notification>> GetNotificationsPendingTranslationAsync(int take)
    {
        var retryBefore = DateTime.UtcNow.AddMinutes(-15);
        return _context.Notifications
            .Where(x =>
                ((x.TranslationStatusCode == null
                  || x.TranslationStatusCode != "translated")
                 && (x.TranslatedAt == null || x.TranslatedAt < retryBefore))
                || x.TitleVi == null
                || x.TitleEn == null
                || x.BodyVi == null
                || x.BodyEn == null)
            .OrderBy(x => x.TranslatedAt ?? DateTime.MinValue)
            .ThenBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public Task<DeviceToken?> GetDeviceTokenByTokenAsync(string fcmToken)
    {
        return _context.DeviceTokens
            .FirstOrDefaultAsync(x => x.FcmToken == fcmToken);
    }

    public Task<List<DeviceToken>> GetActiveDeviceTokensForUserAsync(Guid userId)
    {
        return _context.DeviceTokens
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync();
    }

    public Task AddNotificationAsync(Notification notification)
    {
        return _context.Notifications.AddAsync(notification).AsTask();
    }

    public Task AddUserNotificationAsync(UserNotification userNotification)
    {
        return _context.UserNotifications.AddAsync(userNotification).AsTask();
    }

    public Task AddUserNotificationsAsync(IEnumerable<UserNotification> userNotifications)
    {
        return _context.UserNotifications.AddRangeAsync(userNotifications);
    }

    public Task AddDeviceTokenAsync(DeviceToken deviceToken)
    {
        return _context.DeviceTokens.AddAsync(deviceToken).AsTask();
    }

    public void UpdateUserProfile(UserProfile userProfile)
    {
        _context.UserProfiles.Update(userProfile);
    }

    public void UpdateUserNotification(UserNotification userNotification)
    {
        _context.UserNotifications.Update(userNotification);
    }

    public void UpdateNotification(Notification notification)
    {
        _context.Notifications.Update(notification);
    }

    public void UpdateDeviceToken(DeviceToken deviceToken)
    {
        _context.DeviceTokens.Update(deviceToken);
    }

    public async Task DeleteNotificationAsync(Notification notification)
    {
        var userNotifications = await _context.UserNotifications
            .Where(x => x.NotificationId == notification.NotificationId)
            .ToListAsync();

        _context.UserNotifications.RemoveRange(userNotifications);
        _context.Notifications.Remove(notification);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
