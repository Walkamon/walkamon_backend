using DAL.DTO;
using DAL.Models;

namespace DAL.Interfaces;

public interface INotificationRepository
{
    Task<UserProfile?> GetUserProfileAsync(Guid userId);

    Task<(List<UserNotification> Items, int TotalCount)> GetUserNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? typeCode,
        bool? isRead);

    Task<UserNotification?> GetUserNotificationAsync(
        Guid userId,
        Guid notificationId);

    Task<(List<Notification> Items, int TotalCount)> GetAdminNotificationsAsync(
        int page,
        int pageSize,
        string? search,
        string? targetAudienceCode,
        string? statusCode,
        string? sortBy,
        bool sortDescending);

    Task<Notification?> GetNotificationByIdAsync(Guid notificationId);

    Task<List<Guid>> GetTargetAudienceUserIdsAsync(
        string targetAudienceCode,
        DateTime now);

    Task<List<DeviceToken>> GetActiveDeviceTokensForUsersAsync(
        IReadOnlyCollection<Guid> userIds);

    Task<List<Notification>> GetDueScheduledNotificationsAsync(
        DateTime now,
        int take);

    Task<List<Notification>> GetNotificationsPendingTranslationAsync(int take);

    Task<DeviceToken?> GetDeviceTokenByTokenAsync(string fcmToken);

    Task<List<DeviceToken>> GetActiveDeviceTokensForUserAsync(Guid userId);

    Task AddNotificationAsync(Notification notification);

    Task AddUserNotificationAsync(UserNotification userNotification);

    Task AddUserNotificationsAsync(IEnumerable<UserNotification> userNotifications);

    Task AddDeviceTokenAsync(DeviceToken deviceToken);

    void UpdateUserProfile(UserProfile userProfile);

    void UpdateUserNotification(UserNotification userNotification);

    void UpdateNotification(Notification notification);

    void UpdateDeviceToken(DeviceToken deviceToken);

    Task DeleteNotificationAsync(Notification notification);

    Task SaveChangesAsync();
}
