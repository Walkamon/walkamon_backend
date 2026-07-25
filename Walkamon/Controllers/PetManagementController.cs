using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/admin/pets")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class PetManagementController : ControllerBase
    {
        private readonly IPetService _petService;

        public PetManagementController(IPetService petService)
        {
            _petService = petService;
        }

       
        [HttpGet]
        public async Task<IActionResult> GetAllPets()
        {
            var result = await _petService.GetAllPetsAsync();

            return Ok(new ApiResponse<List<PetListResponse>>
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get pet list successfully.",
                Data = result
            });
        }

      
        [HttpGet("{petId:guid}")]
        public async Task<IActionResult> GetPetDetail(Guid petId)
        {
            var result = await _petService.GetPetDetailAsync(petId);

            return Ok(new ApiResponse<PetDetailResponse>
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Get pet detail successfully.",
                Data = result
            });
        }

     
        [HttpPut("{petId:guid}")]
        public async Task<IActionResult> UpdatePet(
            Guid petId,
            [FromBody] UpdatePetRequest request)
        {
            await _petService.UpdatePetAsync(petId, request);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Status = StatusCodes.Status200OK,
                Message = "Pet updated successfully.",
                Data = null
            });
        }
    }
}
