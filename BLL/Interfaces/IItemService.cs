using DAL.DTO;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.Interfaces
{
    public interface IItemService
    {
        Task<IEnumerable<ItemResponse>> GetAllAsync();
        Task<Item?> GetByIdAsync(Guid id);
        Task<ItemResponse?> CreateAsync(CreateItemRequest request, string? imageUrl);
        Task<ItemResponse> UpdateAsync(Guid id, UpdateItemRequest request, string? imageUrl);
        Task UpdateStatusAsync(Guid id, bool isActive);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<ItemResponse>> GetAllActiveAsync();
    }
}
