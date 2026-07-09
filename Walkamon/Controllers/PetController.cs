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
    }
}
