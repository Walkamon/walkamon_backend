using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/item-types")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class ItemTypeControllerForAdmin : ControllerBase
    {
        private readonly IItemTypeService _itemTypeService;

        public ItemTypeControllerForAdmin(IItemTypeService itemTypeService)
        {
            _itemTypeService = itemTypeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _itemTypeService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _itemTypeService.GetByIdAsync(id);

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateItemTypeRequest request)
        {
            var result = await _itemTypeService.CreateAsync(request);

            return Ok(new
            {
                Data = result,
                Message = "Create Type Item successfully"
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateItemTypeRequest request)
        {
            var result = await _itemTypeService.UpdateAsync(id, request);

            return Ok(new
            {
                Data = result,
                Message = "update Type Item successfully"
            });
        }

        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _itemTypeService.DeleteAsync(id);

            return Ok(new
            {
                Message = "Delete Type Item successfully"
            });
        }
    }
}
