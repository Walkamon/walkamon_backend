using DAL.DTO;

namespace BLL.Interfaces;

public interface IShopService
{
    Task<List<ShopItemListResponse>> GetShopItemsAsync();

    Task<ShopItemDetailResponse> GetShopItemDetailAsync(Guid shopItemId);

    Task<BuyShopItemResponse> BuyShopItemAsync(
        Guid userId,
        BuyShopItemRequest request);
}
