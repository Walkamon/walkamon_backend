using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/missions")]
public class PlayerMissionController : BaseController
{
    private readonly IPlayerMissionService _playerMissionService;

    public PlayerMissionController(IPlayerMissionService playerMissionService)
    {
        _playerMissionService = playerMissionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PlayerMissionListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllMissions()
    {
        var result = await _playerMissionService.GetAllMissionsAsync(CurrentUserId);

        return Ok(new ApiResponse<PlayerMissionListResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get missions success",
            Data = result
        });
    }

    [HttpGet("daily")]
    [ProducesResponseType(typeof(ApiResponse<List<PlayerMissionItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDailyMissions()
    {
        var result = await _playerMissionService.GetDailyMissionsAsync(CurrentUserId);

        return Ok(new ApiResponse<List<PlayerMissionItemResponse>>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get daily missions success",
            Data = result
        });
    }

    [HttpPost("{missionId:guid}/claim")]
    [ProducesResponseType(typeof(ApiResponse<ClaimMissionRewardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClaimMissionReward(Guid missionId)
    {
        var result = await _playerMissionService.ClaimMissionRewardAsync(
            CurrentUserId,
            missionId);

        return Ok(new ApiResponse<ClaimMissionRewardResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Claim mission reward success",
            Data = result
        });
    }
}
