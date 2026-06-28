using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/achievements")]
public class AdminAchievementController : ControllerBase
{
    private readonly IAdminAchievementService _adminAchievementService;
    private readonly ICloudinaryService _cloudinaryService;

    public AdminAchievementController(
        IAdminAchievementService adminAchievementService,
        ICloudinaryService cloudinaryService)
    {
        _adminAchievementService = adminAchievementService;
        _cloudinaryService = cloudinaryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminAchievementListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAchievements()
    {
        var result = await _adminAchievementService.GetAchievementsAsync();

        return Ok(new ApiResponse<AdminAchievementListResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get achievements success",
            Data = result
        });
    }

    [HttpGet("{achievementId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminAchievementDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAchievementDetail(Guid achievementId)
    {
        var result = await _adminAchievementService.GetAchievementDetailAsync(
            achievementId);

        return Ok(new ApiResponse<AdminAchievementDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get achievement detail success",
            Data = result
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminAchievementDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAchievement(
        [FromForm] CreateAdminAchievementRequest request)
    {
        string? iconUrl = null;

        if (request.Icon != null)
        {
            iconUrl = await _cloudinaryService.UploadImageAsync(request.Icon);
        }

        var result = await _adminAchievementService.CreateAchievementAsync(
            request, iconUrl);

        return StatusCode(
            StatusCodes.Status201Created,
            new ApiResponse<AdminAchievementDetailResponse>
            {
                Success = true,
                Status = StatusCodes.Status201Created,
                Message = "Create achievement success",
                Data = result
            });
    }

    [HttpPut("{achievementId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminAchievementDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAchievement(
        Guid achievementId,
        [FromForm] UpdateAdminAchievementRequest request)
    {
        string? iconUrl = null;

        if (request.Icon != null)
        {
            iconUrl = await _cloudinaryService.UploadImageAsync(request.Icon);
        }

        var result = await _adminAchievementService.UpdateAchievementAsync(
            achievementId, request, iconUrl);

        return Ok(new ApiResponse<AdminAchievementDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update achievement success",
            Data = result
        });
    }

    [HttpPatch("{achievementId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAchievementStatus(
        Guid achievementId,
        UpdateAdminAchievementStatusRequest request)
    {
        await _adminAchievementService.UpdateAchievementStatusAsync(
            achievementId, request);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update achievement status success",
            Data = null
        });
    }
}
