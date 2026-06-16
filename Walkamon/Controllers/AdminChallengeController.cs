using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/challenges")]
public class AdminChallengeController : ControllerBase
{
    private readonly IAdminChallengeService _adminChallengeService;

    public AdminChallengeController(IAdminChallengeService adminChallengeService)
    {
        _adminChallengeService = adminChallengeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminChallengeListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChallenges(
        [FromQuery] string? search,
        [FromQuery] string? status)
    {
        var result = await _adminChallengeService.GetChallengesAsync(
            search,
            status);

        return Ok(new ApiResponse<AdminChallengeListResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get challenges success",
            Data = result
        });
    }

    [HttpGet("{challengeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminChallengeDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChallengeDetail(Guid challengeId)
    {
        var result = await _adminChallengeService.GetChallengeDetailAsync(
            challengeId);

        return Ok(new ApiResponse<AdminChallengeDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get challenge detail success",
            Data = result
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminChallengeDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateChallenge(
        CreateAdminChallengeRequest request)
    {
        var result = await _adminChallengeService.CreateChallengeAsync(request);

        return StatusCode(
            StatusCodes.Status201Created,
            new ApiResponse<AdminChallengeDetailResponse>
            {
                Success = true,
                Status = StatusCodes.Status201Created,
                Message = "Create challenge success",
                Data = result
            });
    }

    [HttpPut("{challengeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminChallengeDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChallenge(
        Guid challengeId,
        UpdateAdminChallengeRequest request)
    {
        var result = await _adminChallengeService.UpdateChallengeAsync(
            challengeId,
            request);

        return Ok(new ApiResponse<AdminChallengeDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update challenge success",
            Data = result
        });
    }

    [HttpPatch("{challengeId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChallengeStatus(
        Guid challengeId,
        UpdateAdminChallengeStatusRequest request)
    {
        await _adminChallengeService.UpdateChallengeStatusAsync(
            challengeId,
            request);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update challenge status success",
            Data = null
        });
    }

}
