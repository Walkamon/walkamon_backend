using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/missions")]
public class AdminMissionController : ControllerBase
{
    private readonly IAdminMissionService _adminMissionService;

    public AdminMissionController(IAdminMissionService adminMissionService)
    {
        _adminMissionService = adminMissionService;
    }

    [HttpGet("daily")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDailyMissions()
    {
        var result = await _adminMissionService.GetDailyMissionsAsync();

        return Ok(new ApiResponse<AdminMissionListResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get daily missions success",
            Data = result
        });
    }

    [HttpGet("daily/{missionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDailyMissionDetail(Guid missionId)
    {
        var result = await _adminMissionService.GetDailyMissionDetailAsync(
            missionId);

        return Ok(new ApiResponse<AdminMissionDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get daily mission detail success",
            Data = result
        });
    }

    [HttpPost("daily")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDailyMission(
        CreateAdminMissionRequest request)
    {
        var result = await _adminMissionService.CreateDailyMissionAsync(request);

        return StatusCode(
            StatusCodes.Status201Created,
            new ApiResponse<AdminMissionDetailResponse>
            {
                Success = true,
                Status = StatusCodes.Status201Created,
                Message = "Create daily mission success",
                Data = result
            });
    }

    [HttpPut("daily/{missionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDailyMission(
        Guid missionId,
        UpdateAdminMissionRequest request)
    {
        var result = await _adminMissionService.UpdateDailyMissionAsync(
            missionId,
            request);

        return Ok(new ApiResponse<AdminMissionDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update daily mission success",
            Data = result
        });
    }

    [HttpPatch("daily/{missionId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDailyMissionStatus(
        Guid missionId,
        UpdateAdminMissionStatusRequest request)
    {
        await _adminMissionService.UpdateDailyMissionStatusAsync(
            missionId,
            request);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update daily mission status success",
            Data = null
        });
    }

    [HttpGet("overall")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverallMissions()
    {
        var result = await _adminMissionService.GetOverallMissionsAsync();

        return Ok(new ApiResponse<AdminMissionListResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get overall missions success",
            Data = result
        });
    }

    [HttpGet("overall/{missionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverallMissionDetail(Guid missionId)
    {
        var result = await _adminMissionService.GetOverallMissionDetailAsync(
            missionId);

        return Ok(new ApiResponse<AdminMissionDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get overall mission detail success",
            Data = result
        });
    }

    [HttpPost("overall")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateOverallMission(
        CreateAdminMissionRequest request)
    {
        var result = await _adminMissionService.CreateOverallMissionAsync(request);

        return StatusCode(
            StatusCodes.Status201Created,
            new ApiResponse<AdminMissionDetailResponse>
            {
                Success = true,
                Status = StatusCodes.Status201Created,
                Message = "Create overall mission success",
                Data = result
            });
    }

    [HttpPut("overall/{missionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminMissionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOverallMission(
        Guid missionId,
        UpdateAdminMissionRequest request)
    {
        var result = await _adminMissionService.UpdateOverallMissionAsync(
            missionId,
            request);

        return Ok(new ApiResponse<AdminMissionDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update overall mission success",
            Data = result
        });
    }

    [HttpPatch("overall/{missionId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOverallMissionStatus(
        Guid missionId,
        UpdateAdminMissionStatusRequest request)
    {
        await _adminMissionService.UpdateOverallMissionStatusAsync(
            missionId,
            request);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update overall mission status success",
            Data = null
        });
    }
}
