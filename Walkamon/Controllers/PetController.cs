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

        [HttpPost("exp")]
        public async Task<IActionResult> AddPetExp()
        {
          

            var result = await _petService.AddPetExpAsync(
                CurrentUserId);

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
        [HttpGet("evolution/options")]
        public async Task<IActionResult> GetEvolutionOptions()
        {
            var result = await _petService
                .GetEvolutionOptionsAsync(CurrentUserId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Data = result
            });
        }
        [HttpPost("evolution")]
        public async Task<IActionResult> Evolve(
    EvolutionRequest request)
        {
            await _petService.EvolveStarterAsync(
                CurrentUserId,
                request.PetId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Evolution successful."
            });
        }
        [HttpGet("evolution/stages")]
        public async Task<IActionResult> GetStages()
        {
            var result = await _petService
                .GetEvolutionStagesAsync(CurrentUserId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get evolution stages successfully.",
                Data = result
            });
        }
        [HttpPost("evolution/next")]
        public async Task<IActionResult> EvolveNext()
        {
            var result = await _petService.EvolveNextAsync(CurrentUserId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Evolution successful.",
                Data = result
            });
        }
        [HttpGet("leaderboard")]
public async Task<IActionResult> GetLeaderboard()
{
    var result = await _petService.GetLeaderboardAsync();

    return Ok(new
    {
        Success = true,
        Status = StatusCodes.Status200OK,
        Message = "Get leaderboard successfully.",
        Data = result
    });
}
        [HttpGet("evolution/history")]
        public async Task<IActionResult> GetHistory()
        {
            var result = await _petService.GetEvolutionHistoryAsync(CurrentUserId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get evolution history successfully.",
                Data = result
            });
        }

        [HttpGet("friend/{friendUserId:guid}")]
        public async Task<IActionResult> GetFriendSpirit(Guid friendUserId)
        {
            var result = await _petService.GetFriendSpiritAsync(friendUserId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get friend spirit successfully.",
                Data = result
            });
        }
        [HttpGet("evolution/preview")]
        public async Task<IActionResult> GetEvolutionPreview()
        {
            var result = await _petService
                .GetEvolutionPreviewAsync();

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get evolution preview successfully.",
                Data = result
            });
        }
        [HttpGet("current-animation")]
        public async Task<IActionResult> GetCurrentAnimation()
        {
            var result = await _petService.GetCurrentAnimationAsync(CurrentUserId);

            return Ok(new
            {
                Success = true,
                Status = 200,
                Message = "Get current animation successfully.",
                Data = result
            });
        }

        [HttpGet("pet-name")]
        public async Task<IActionResult> GetUserPetName()
        {
            var result = await _petService.GetUserPetNameAsync(CurrentUserId);

            return Ok(new
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Pet name retrieved successfully.",
                Data = result
            });
        }
    }
}
