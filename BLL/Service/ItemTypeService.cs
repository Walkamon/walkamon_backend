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
        private readonly IGenericRepository<Item> _itemRepository;

        public ItemTypeService(
        IGenericRepository<ItemType> itemTypeRepository,
        IGenericRepository<Item> itemRepository)
        {
            _itemTypeRepository = itemTypeRepository;
            _itemRepository = itemRepository;
        }

        public async Task<IEnumerable<ItemTypeResponse>> GetAllAsync()
        {
            var itemTypes = await _itemTypeRepository.GetAllAsync();
            var items = await _itemRepository.GetAllAsync();

            var itemCounts = items
                .GroupBy(x => x.ItemTypeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count());

            return itemTypes.Select(x => new ItemTypeResponse
            {
                ItemTypeId = x.ItemTypeId,
                ItemTypeName = x.ItemTypeName,
                IsActive = x.IsActive,
                count = itemCounts.TryGetValue(
                    x.ItemTypeId,
                    out var count)
                        ? count
                        : 0
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
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _itemTypeRepository.AddAsync(itemType);
            await _itemTypeRepository.SaveAsync();

            return new ItemTypeResponse
            {
                ItemTypeId = itemType.ItemTypeId,
                ItemTypeName = itemType.ItemTypeName,
                IsActive = itemType.IsActive
              
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
                IsActive = itemType.IsActive
             
            };
        }

        public async Task DeleteAsync(Guid id)
        {
            await UpdateStatusAsync(id, false);
        }

        public async Task UpdateStatusAsync(Guid id, bool isActive)
        {
            var itemType = await _itemTypeRepository.GetByIdAsync(id);

            if (itemType == null)
                throw new NotFoundException("Item type not found");

            itemType.IsActive = isActive;
            itemType.UpdatedAt = DateTime.UtcNow;

            _itemTypeRepository.Update(itemType);

         
            if (!isActive)
            {
                var items = await _itemRepository.FindAsync(x => x.ItemTypeId == id);

                foreach (var item in items)
                {
                    item.IsActive = false;
                  
                    _itemRepository.Update(item);
                }
            }
            if (isActive)
            {
                var items = await _itemRepository.FindAsync(x => x.ItemTypeId == id);

                foreach (var item in items)
                {
                    item.IsActive = true;
                
                    _itemRepository.Update(item);
                }
            }

            await _itemTypeRepository.SaveAsync();
        }

        public async Task<IEnumerable<ItemTypeResponse>> GetAllActive()
        {
            var itemTypes = await _itemTypeRepository
    .FindAsync(x => x.IsActive);

            var items = await _itemRepository.GetAllAsync();

            var itemCounts = items
                .GroupBy(x => x.ItemTypeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count());

            return itemTypes.Select(x => new ItemTypeResponse
            {
                ItemTypeId = x.ItemTypeId,
                ItemTypeName = x.ItemTypeName,
                IsActive = x.IsActive,
                count = itemCounts.TryGetValue(
                    x.ItemTypeId,
                    out var count)
                        ? count
                        : 0
            }).ToList();
        
        }
    }
}
