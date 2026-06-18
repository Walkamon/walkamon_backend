using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/challenges")]
public class PlayerChallengeController : BaseController
{
    private readonly IPlayerChallengeService _playerChallengeService;

    public PlayerChallengeController(
        IPlayerChallengeService playerChallengeService)
    {
        _playerChallengeService = playerChallengeService;
    }

    [HttpGet("random")]
    [ProducesResponseType(typeof(ApiResponse<PlayerChallengeStateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRandomChallenge()
    {
        var result = await _playerChallengeService
            .GetRandomChallengeStateAsync(CurrentUserId);

        return Ok(new ApiResponse<PlayerChallengeStateResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get random challenge success",
            Data = result
        });
    }

    [HttpPost("random")]
    [ProducesResponseType(typeof(ApiResponse<PlayerChallengeStateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateRandomChallenge()
    {
        var result = await _playerChallengeService
            .CreateRandomChallengeAsync(CurrentUserId);

        return Ok(new ApiResponse<PlayerChallengeStateResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Create random challenge success",
            Data = result
        });
    }

    [HttpPatch("{userMissionId:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<CancelPlayerChallengeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelChallenge(Guid userMissionId)
    {
        var result = await _playerChallengeService.CancelChallengeAsync(
            CurrentUserId,
            userMissionId);

        return Ok(new ApiResponse<CancelPlayerChallengeResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Cancel challenge success",
            Data = result
        });
    }
}
