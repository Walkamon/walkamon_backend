using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notifications")]
public class AdminNotificationController : BaseController
{
    private readonly INotificationService _notificationService;

    public AdminNotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminNotificationListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? targetAudienceCode = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] string? sortBy = "created_at",
        [FromQuery] string? sortDirection = "desc")
    {
        var result = await _notificationService.GetAdminNotificationsAsync(
            page,
            pageSize,
            search,
            targetAudienceCode,
            statusCode,
            sortBy,
            sortDirection);

        return Ok(new ApiResponse<AdminNotificationListResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get admin notifications success",
            Data = result
        });
    }

    [HttpGet("{notificationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminNotificationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotificationDetail(Guid notificationId)
    {
        var result =
            await _notificationService.GetAdminNotificationDetailAsync(
                notificationId);

        return Ok(new ApiResponse<AdminNotificationDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get admin notification detail success",
            Data = result
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminNotificationDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateNotification(
        CreateAdminNotificationRequest request)
    {
        var result = await _notificationService.CreateAdminNotificationAsync(
            CurrentUserId,
            request);

        return StatusCode(
            StatusCodes.Status201Created,
            new ApiResponse<AdminNotificationDetailResponse>
            {
                Success = true,
                Status = StatusCodes.Status201Created,
                Message = "Create notification success",
                Data = result
            });
    }

    [HttpPut("{notificationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminNotificationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNotification(
        Guid notificationId,
        UpdateAdminNotificationRequest request)
    {
        var result = await _notificationService.UpdateAdminNotificationAsync(
            notificationId,
            request);

        return Ok(new ApiResponse<AdminNotificationDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update notification success",
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
        await _notificationService.DeleteAdminNotificationAsync(notificationId);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Delete notification success",
            Data = null
        });
    }
}
