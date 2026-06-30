using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Walkamon.Controllers
{
    [ApiController]
    [Route("api/Static-steps")]
    [Authorize(Roles = "User")]
    public class StaticDailyStepController :BaseController
    {
        private readonly IDailyStepService _dailyStepService;

        public StaticDailyStepController(
            IDailyStepService dailyStepService)
        {
            _dailyStepService = dailyStepService;
        }
        [HttpGet("daily")]
        public async Task<IActionResult> Daily([FromQuery] DateOnly? date)
        {
            

            return Ok(await _dailyStepService.GetDailyStatisticAsync(CurrentUserId, date));
        }

        [HttpGet("weekly")]
        public async Task<IActionResult> Weekly([FromQuery] DateOnly? date)
        {
         

            return Ok(await _dailyStepService.GetWeeklyStatisticAsync(CurrentUserId, date));
        }

        [HttpGet("monthly")]
        public async Task<IActionResult> Monthly([FromQuery] DateOnly? date)
        {

            return Ok(await _dailyStepService.GetMonthlyStatisticAsync(CurrentUserId, date));
        }
    }
}
