using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/inventory")]
public class InventoryController : BaseController
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<InventoryItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInventory()
    {
        var result = await _inventoryService.GetInventoryAsync(CurrentUserId);

        return Ok(new ApiResponse<List<InventoryItemResponse>>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get inventory success",
            Data = result
        });
    }

    [HttpGet("items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemDetail(Guid itemId)
    {
        var result = await _inventoryService.GetInventoryItemDetailAsync(
            CurrentUserId,
            itemId);

        return Ok(new ApiResponse<InventoryItemResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get inventory item detail success",
            Data = result
        });
    }

    [HttpPost("use")]
    [ProducesResponseType(typeof(ApiResponse<UseItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UseItem(UseItemRequest request)
    {
        var result = await _inventoryService.UseItemAsync(
            CurrentUserId,
            request);

        return Ok(new ApiResponse<UseItemResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Use item success",
            Data = result
        });
    }
}
