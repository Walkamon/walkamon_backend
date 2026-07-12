using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/daily-login-rewards")]
public class DailyLoginRewardController : BaseController
{
    private readonly IDailyLoginRewardService _dailyLoginRewardService;

    public DailyLoginRewardController(IDailyLoginRewardService dailyLoginRewardService)
    {
        _dailyLoginRewardService = dailyLoginRewardService;
    }

    [HttpGet("calendar")]
    [ProducesResponseType(typeof(ApiResponse<DailyLoginRewardCalendarResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCalendar()
    {
        var result = await _dailyLoginRewardService.GetCalendarAsync(CurrentUserId);

        return Ok(new ApiResponse<DailyLoginRewardCalendarResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get daily login reward calendar success",
            Data = result
        });
    }

    [HttpPost("claim")]
    [ProducesResponseType(typeof(ApiResponse<DailyLoginRewardClaimResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Claim()
    {
        var result = await _dailyLoginRewardService.ClaimAsync(CurrentUserId);

        return Ok(new ApiResponse<DailyLoginRewardClaimResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Claim daily login reward success",
            Data = result
        });
    }
}
