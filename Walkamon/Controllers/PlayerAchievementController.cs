using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/achievements")]
public class PlayerAchievementController : BaseController
{
    private readonly IPlayerAchievementService _playerAchievementService;

    public PlayerAchievementController(IPlayerAchievementService playerAchievementService)
    {
        _playerAchievementService = playerAchievementService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PlayerAchievementItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAchievements()
    {
        var result = await _playerAchievementService.GetAchievementsAsync(CurrentUserId);

        return Ok(new ApiResponse<List<PlayerAchievementItemResponse>>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get achievements success",
            Data = result
        });
    }

    [HttpGet("{achievementId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PlayerAchievementItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAchievementDetail(Guid achievementId)
    {
        var result = await _playerAchievementService.GetAchievementDetailAsync(CurrentUserId, achievementId);

        return Ok(new ApiResponse<PlayerAchievementItemResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get achievement detail success",
            Data = result
        });
    }

    [HttpPost("{achievementId:guid}/claim")]
    [ProducesResponseType(typeof(ApiResponse<ClaimAchievementRewardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClaimAchievementReward(Guid achievementId)
    {
        var result = await _playerAchievementService.ClaimAchievementRewardAsync(
            CurrentUserId, achievementId);

        return Ok(new ApiResponse<ClaimAchievementRewardResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Claim achievement reward success",
            Data = result
        });
    }
}
