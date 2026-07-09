using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "User")]
    public class PetController : BaseController
    {
        private readonly IPetService _petService;

        public PetController(
            IPetService petService)
        {
            _petService = petService;
        }

        [HttpPost("create-stater-pet")]
        public async Task<IActionResult> CreatePet(
    [FromBody] CreateUserPetRequest request)
        {
            

            await _petService.CreateUserPetAsync(
                CurrentUserId,
                request);

            return Ok(new 
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Pet created successfully."
            });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetPetStatus()
        {
          

            var result = await _petService.GetPetStatusAsync(CurrentUserId);

            return Ok(new 
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Pet status retrieved successfully.",
                Data = result
            });
        }

        [HttpPost("exp/{exp:int}")]
        public async Task<IActionResult> AddPetExp(
    int exp)
        {
          

            var result = await _petService.AddPetExpAsync(
                CurrentUserId,
                exp);

            return Ok(new 
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Pet exp added successfully.",
                Data = result
            });
        }
        [HttpPost("tap")]
        public async Task<IActionResult> TapSpirit()
        {
            var result = await _petService.TapSpiritAsync(CurrentUserId);

            return Ok(new ApiResponse<PetStatusResponse>
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Tap spirit successfully.",
                Data = result
            });
        }

        [HttpPost("feed")]
        public async Task<IActionResult> FeedSpirit()
        {
            var result = await _petService.FeedSpiritAsync(CurrentUserId);

            return Ok(new ApiResponse<PetStatusResponse>
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Feed spirit successfully.",
                Data = result
            });
        }
        [HttpGet("info")]
        public async Task<IActionResult> GetPetInfo()
        {
            var result = await _petService.GetPetInfoAsync(CurrentUserId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get pet information successfully.",
                Data = result
            });
        }
    }
}
