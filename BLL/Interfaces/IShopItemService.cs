using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IShopItemService
    {
        Task<List<ShopItemResponse>> GetAllAsync();

        Task<ShopItemResponse?> GetByIdAsync(Guid id);

        Task<ShopItemResponse> CreateAsync(ShopItemRequest request);

        Task<ShopItemResponse> UpdateAsync(Guid id, ShopItemRequest request);

        Task<bool> ToggleStatusAsync(Guid id);
    }
}
