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
        public IActionResult UpdateStep(
            [FromBody] UpdateDailyStepRequest request)
        {
            return StatusCode(StatusCodes.Status410Gone, new
            {
                Message = "Manual step entry is disabled. Use the validated physical-step sync pipeline."
            });
        }


    }
}
