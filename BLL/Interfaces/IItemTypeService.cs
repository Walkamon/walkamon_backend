using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAL.Models;
namespace BLL.Interfaces
{
    public interface IItemTypeService
    {
        Task<IEnumerable<ItemTypeResponse>> GetAllAsync();
        Task<ItemType?> GetByIdAsync(Guid id);
        Task<ItemTypeResponse> CreateAsync(CreateItemTypeRequest request);
        Task<ItemTypeResponse> UpdateAsync(Guid id, UpdateItemTypeRequest request);
        Task UpdateStatusAsync(Guid id, bool isActive);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<ItemTypeResponse>> GetAllActive();
    }
}
