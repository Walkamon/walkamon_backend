using BLL.Interfaces;
using BLL.Service;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [ApiController]
    [Route("api/Step-Goal")]
    [Authorize(Roles = "User")]
    public class StepGoalController :BaseController
    {
        private readonly IStepGoalService _stepGoalService;
        private readonly IStreakRewardService _streakRewardService;
        public StepGoalController(IStepGoalService service, IStreakRewardService streakRewardService)
        {
            _stepGoalService = service;
            _streakRewardService = streakRewardService;
        }
        [HttpPost]
        public async Task<IActionResult> SetGoal(
    SetStepGoalRequest request)
        {
         

            await _stepGoalService.SetGoalAsync(
                CurrentUserId,
                request);

            return Ok(new 
            {
                Message = "Step goal saved successfully."
            });
        }

        [HttpGet("progress")]
        public async Task<IActionResult> GetGoalProgress()
        {
   
            var result = await _stepGoalService
                .GetGoalProgressAsync(CurrentUserId);

            return Ok(new ApiResponse<GoalProgressResponse>
            {
                Message = "Goal progress retrieved successfully.",
                Data = result
            });
        }

        [HttpGet("longest-streak")]
        public async Task<IActionResult> GetLongestStreak()
        {
           
            var result = await _stepGoalService.GetLongestStreakAsync(CurrentUserId);

            return Ok(new ApiResponse<LongestStreakResponse>
            {
                Message = "Longest streak retrieved successfully.",
                Data = result
            });
        }
        [HttpGet("current-streak")]
        public async Task<IActionResult> GetCurrentStreak()
        {
      

            var result = await _stepGoalService.GetCurrentStreakAsync(CurrentUserId);

            return Ok(new ApiResponse<CurrentStreakResponse>
            {
                Message = "Current streak retrieved successfully.",
                Data = result
            });
        }

        [HttpPost("claim")]
        public async Task<IActionResult> ClaimReward()
        {

            var streak = await _stepGoalService.GetCurrentStreakAsync(CurrentUserId);

            var result = await _streakRewardService
                .ClaimRewardAsync(CurrentUserId,streak);

            return Ok(new ApiResponse<ClaimRewardResponse>
            {
                Message = "Reward claimed successfully.",
                Data = result
            });
        }
    }
}
