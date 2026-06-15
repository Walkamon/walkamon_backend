using DAL.DTO;

namespace BLL.Interfaces;

public interface IInventoryService
{
    Task<List<InventoryItemResponse>> GetInventoryAsync(Guid userId);

    Task<InventoryItemResponse> GetInventoryItemDetailAsync(
        Guid userId,
        Guid itemId);

    Task<UseItemResponse> UseItemAsync(
        Guid userId,
        UseItemRequest request);
}
