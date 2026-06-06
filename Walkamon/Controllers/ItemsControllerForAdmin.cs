using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [ApiController]
    [Route("api/items")]
    [Authorize(Roles = "Admin")]
    public class ItemsControllerForAdmin : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemsControllerForAdmin(IItemService itemService)
        {
            _itemService = itemService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _itemService.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            return Ok(await _itemService.GetByIdAsync(id));
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateItemRequest dto)
        {
            return Ok(await _itemService.CreateAsync(dto));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateItemRequest dto)
        {
            return Ok(await _itemService.UpdateAsync(id, dto));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _itemService.DeleteAsync(id);
            return NoContent();
        }
    }
}
