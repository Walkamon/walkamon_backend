using BLL.Interfaces;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BLL.Exceptions;
namespace BLL.Service
{
    public class ItemTypeService : IItemTypeService
    {
        private readonly IGenericRepository<ItemType> _itemTypeRepository;

        public ItemTypeService(IGenericRepository<ItemType> itemTypeRepository)
        {
            _itemTypeRepository = itemTypeRepository;
        }

        public async Task<IEnumerable<ItemTypeResponse>> GetAllAsync()
        {
            var itemTypes = await _itemTypeRepository.GetAllAsync();

            return itemTypes.Select(x => new ItemTypeResponse
            {
                ItemTypeId = x.ItemTypeId,
                ItemTypeName = x.ItemTypeName,
               
            }).ToList();
        }

        public async Task<ItemType?> GetByIdAsync(Guid id)
        {
            var itemType = await _itemTypeRepository.GetByIdAsync(id);

            if (itemType == null)
                return null;

            return itemType;
        }

        public async Task<ItemTypeResponse> CreateAsync(CreateItemTypeRequest request)
        {
            bool exists = await _itemTypeRepository.AnyAsync(x =>
                x.ItemTypeName.ToLower() == request.ItemTypeName.ToLower());

            if (exists)
                throw new ConflictException("Item type already exists");

            var itemType = new ItemType
            {
                ItemTypeId = Guid.NewGuid(),
                ItemTypeName = request.ItemTypeName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _itemTypeRepository.AddAsync(itemType);
            await _itemTypeRepository.SaveAsync();

            return new ItemTypeResponse
            {
                ItemTypeId = itemType.ItemTypeId,
                ItemTypeName = itemType.ItemTypeName,
              
            };
        }

        public async Task<ItemTypeResponse> UpdateAsync(
            Guid id,
            UpdateItemTypeRequest request)
        {
            var itemType = await _itemTypeRepository.GetByIdAsync(id);

            if (itemType == null)
                throw new NotFoundException("Item type not found");

            bool exists = await _itemTypeRepository.AnyAsync(x =>
                x.ItemTypeId != id &&
                x.ItemTypeName.ToLower() == request.ItemTypeName.ToLower());

            if (exists)
                throw new ConflictException("Item type already exists");

            itemType.ItemTypeName = request.ItemTypeName;
            itemType.UpdatedAt = DateTime.UtcNow;

            _itemTypeRepository.Update(itemType);
            await _itemTypeRepository.SaveAsync();

            return new ItemTypeResponse
            {
                ItemTypeId = itemType.ItemTypeId,
                ItemTypeName = itemType.ItemTypeName,
             
            };
        }

        public async Task DeleteAsync(Guid id)
        {
            var itemType = await _itemTypeRepository.GetByIdAsync(id);

            if (itemType == null)
                throw new NotFoundException("Item type not found");

            _itemTypeRepository.Delete(itemType);
            await _itemTypeRepository.SaveAsync();
        }
    }
}
