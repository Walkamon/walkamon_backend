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
        Task<Item?> CreateAsync(CreateItemRequest request);
        Task<Item> UpdateAsync(Guid id, UpdateItemRequest request);
        Task DeleteAsync(Guid id);
    }
}
