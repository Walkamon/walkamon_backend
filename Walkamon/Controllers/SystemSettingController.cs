using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/system-settings")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class SystemSettingController : ControllerBase
    {
        private readonly ISystemSettingService _systemSettingService;

        public SystemSettingController(
            ISystemSettingService systemSettingService)
        {
            _systemSettingService = systemSettingService;
        }

       
        [HttpGet("step-exp-rate")]
        public async Task<IActionResult> GetStepExpRate()
        {
            var result = await _systemSettingService
                .GetStepExpRateAsync();

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Step EXP rate retrieved successfully.",
                Data = result
            });
        }

       
        [HttpPut("step-exp-rate")]
        public async Task<IActionResult> UpdateStepExpRate(
            [FromBody] UpdateStepExpRateRequest request)
        {
            await _systemSettingService
                .UpdateStepExpRateAsync(request);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Step EXP rate updated successfully."
            });
        }
        [HttpGet("pet-status")]
        
        public async Task<IActionResult> GetPetStatusSetting()
        {
            var result = await _systemSettingService.GetPetStatusSettingAsync();

            return Ok(new ApiResponse<PetStatusSettingResponse>
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get pet status settings successfully.",
                Data = result
            });
        }
        [HttpPut("pet-status")]
        
        public async Task<IActionResult> UpdatePetStatusSetting(
    [FromBody] UpdatePetStatusSettingRequest request)
        {
            await _systemSettingService.UpdatePetStatusSettingAsync(request);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Pet status settings updated successfully.",
                Data = null
            });
        }
    }
}