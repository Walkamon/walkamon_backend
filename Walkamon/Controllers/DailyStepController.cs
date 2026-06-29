using BLL.Interfaces;
using DAL.DTO;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [ApiController]
    [Route("api/daily-steps")]
    [Authorize(Roles = "User")]
    public class DailyStepController : BaseController
    {
        private readonly IDailyStepService _dailyStepService;

        public DailyStepController(
            IDailyStepService dailyStepService)
        {
            _dailyStepService = dailyStepService;
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStep(
            [FromBody] UpdateDailyStepRequest request)
        {
            

            await _dailyStepService.UpdateStepAsync(
                CurrentUserId,
                request);

            return Ok(new
            {
                Message = "Step updated successfully"
            });
        }


    }
}
