using BLL.Interfaces;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [ApiController]
    [Route("api/Leaderboard")]
    [Authorize(Roles = "User")]
    public class LeaderboardController : BaseController
    {
        private readonly IDailyStepService _dailyStepService;

        public LeaderboardController(
            IDailyStepService dailyStepService)
        {
            _dailyStepService = dailyStepService;
        }
        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard(
    [FromQuery] LeaderboardType type,
    [FromQuery] DateOnly? date)
        {
          

            var result = await _dailyStepService.GetLeaderboardAsync(
                CurrentUserId,
                type,
                date);

            return Ok(result);
        }
    }
}
