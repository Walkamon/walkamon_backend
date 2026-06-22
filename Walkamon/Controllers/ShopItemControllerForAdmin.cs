using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/ShopItem")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class ShopItemsController : ControllerBase
    {
        private readonly IShopItemService _service;

        public ShopItemsController(IShopItemService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ShopItemRequest request)
        {
            var result = await _service.CreateAsync(request);

            return Ok(new
            {
                Data = result,
                Message = "Add Shop Item successfully"
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            Guid id,
            ShopItemRequest request)
        {
            var result = await _service.UpdateAsync(id, request);


            return Ok(new
            {
                Data = result,
                Message = "Update Shop Item successfully"
            });
        }

        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _service.ToggleStatusAsync(id);

            if (!result)

                return NotFound(new
                {
                  
                    Message = "Shop Item doesn't exit"
                });


            return Ok(new
            {
                Message = "Delete Shop Item successfully"
            });
        }
    }
}
