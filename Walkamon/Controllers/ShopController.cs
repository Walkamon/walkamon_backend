using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/shop")]
public class ShopController : BaseController
{
    private readonly IShopService _shopService;

    public ShopController(IShopService shopService)
    {
        _shopService = shopService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ShopItemListResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetShopItems()
    {
        var result = await _shopService.GetShopItemsAsync();

        return Ok(new ApiResponse<List<ShopItemListResponse>>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get shop items success",
            Data = result
        });
    }

    [HttpGet("{shopItemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ShopItemDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShopItemDetail(Guid shopItemId)
    {
        var result = await _shopService.GetShopItemDetailAsync(shopItemId);

        return Ok(new ApiResponse<ShopItemDetailResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get shop item detail success",
            Data = result
        });
    }

    [HttpPost("buy")]
    [ProducesResponseType(typeof(ApiResponse<BuyShopItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BuyShopItem(BuyShopItemRequest request)
    {
        var result = await _shopService.BuyShopItemAsync(
            CurrentUserId,
            request);

        return Ok(new ApiResponse<BuyShopItemResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Buy shop item success",
            Data = result
        });
    }
}
