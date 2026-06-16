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
        private readonly ICloudinaryService _cloudinaryService;

        public ItemsControllerForAdmin(
            IItemService itemService,
            ICloudinaryService cloudinaryService)
        {
            _itemService = itemService;
            _cloudinaryService = cloudinaryService;
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
        public async Task<IActionResult> Create([FromForm] CreateItemRequest dto)
        {
            string? imageUrl = null;

            if (dto.Image != null)
            {
                imageUrl = await _cloudinaryService.UploadImageAsync(dto.Image);
            }

            var result = await _itemService.CreateAsync(dto, imageUrl);

            return Ok(new
            {
                Data = result,
                Message = "Create Item Successfully"
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
     Guid id,
     [FromForm] UpdateItemRequest dto)
        {
            string? imageUrl = null;

            if (dto.Image != null)
            {
                imageUrl = await _cloudinaryService.UploadImageAsync(dto.Image);
            }

            var result = await _itemService.UpdateAsync(id, dto, imageUrl);

            return Ok(new
            {
                Data = result,
                Message = "Update Item Successfully"
            });
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateStatus(
            Guid id,
            [FromBody] UpdateActiveStatusRequest request)
        {
            await _itemService.UpdateStatusAsync(id, request.IsActive);

            return Ok(new
            {  
                Message = "Update Item Status Successfully"
            });
        }
    }
}
