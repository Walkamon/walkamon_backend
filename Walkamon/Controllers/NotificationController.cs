using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/notifications")]
public class NotificationController : BaseController
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPatch("settings")]
    [ProducesResponseType(typeof(ApiResponse<NotificationSettingsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSettings(
        NotificationSettingsRequest request)
    {
        var result = await _notificationService.UpdateSettingsAsync(
            CurrentUserId,
            request);

        return Ok(new ApiResponse<NotificationSettingsResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update notification settings success",
            Data = result
        });
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? typeCode = null,
        [FromQuery] bool? isRead = null)
    {
        var result = await _notificationService.GetNotificationsAsync(
            CurrentUserId,
            page,
            pageSize,
            typeCode,
            isRead,
            Request.Headers.AcceptLanguage.ToString());

        return Ok(new ApiResponse<NotificationListResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get notifications success",
            Data = result
        });
    }

    [HttpGet("{notificationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NotificationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotificationDetail(Guid notificationId)
    {
        var result = await _notificationService.GetNotificationDetailAsync(
            CurrentUserId,
            notificationId,
            Request.Headers.AcceptLanguage.ToString());

        return Ok(new ApiResponse<NotificationDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get notification detail success",
            Data = result
        });
    }

    [HttpDelete("{notificationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNotification(Guid notificationId)
    {
        await _notificationService.DeleteNotificationAsync(
            CurrentUserId,
            notificationId);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Delete notification success",
            Data = null
        });
    }

    [HttpPost("device-tokens")]
    [ProducesResponseType(typeof(ApiResponse<DeviceTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpsertDeviceToken(DeviceTokenRequest request)
    {
        var result = await _notificationService.UpsertDeviceTokenAsync(
            CurrentUserId,
            request);

        return Ok(new ApiResponse<DeviceTokenResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Save device token success",
            Data = result
        });
    }

    [HttpPost("device-tokens/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<DeviceTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateDeviceToken(
        DeviceTokenRequest request)
    {
        var result = await _notificationService.DeactivateDeviceTokenAsync(
            CurrentUserId,
            request);

        return Ok(new ApiResponse<DeviceTokenResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Deactivate device token success",
            Data = result
        });
    }
}
