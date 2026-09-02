using DAL.DTO;

namespace BLL.Interfaces;

public interface INotificationService
{
    Task<NotificationSettingsResponse> UpdateSettingsAsync(
        Guid userId,
        NotificationSettingsRequest request);

    Task<NotificationListResponse> GetNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? typeCode,
        bool? isRead,
        string? acceptLanguage = null);

    Task<NotificationDetailResponse> GetNotificationDetailAsync(
        Guid userId,
        Guid notificationId,
        string? acceptLanguage = null);

    Task DeleteNotificationAsync(Guid userId, Guid notificationId);

    Task<DeviceTokenResponse> UpsertDeviceTokenAsync(
        Guid userId,
        DeviceTokenRequest request);

    Task<DeviceTokenResponse> DeactivateDeviceTokenAsync(
        Guid userId,
        DeviceTokenRequest request);

    Task CreateAndSendToUserAsync(
        Guid userId,
        string typeCode,
        string title,
        string body,
        string? imageUrl = null);

    Task<AdminNotificationListResponse> GetAdminNotificationsAsync(
        int page,
        int pageSize,
        string? search,
        string? targetAudienceCode,
        string? statusCode,
        string? sortBy,
        string? sortDirection);

    Task<AdminNotificationDetailResponse> GetAdminNotificationDetailAsync(
        Guid notificationId);

    Task<AdminNotificationDetailResponse> CreateAdminNotificationAsync(
        Guid adminUserId,
        CreateAdminNotificationRequest request);

    Task<AdminNotificationDetailResponse> UpdateAdminNotificationAsync(
        Guid notificationId,
        UpdateAdminNotificationRequest request);

    Task DeleteAdminNotificationAsync(Guid notificationId);

    Task<int> ProcessDueScheduledNotificationsAsync();
}
