using BLL.Interfaces;
using DAL.DTO;
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
    public class ItemService : IItemService
    {
        private readonly IGenericRepository<Item> _itemRepository;
        private readonly IGenericRepository<ItemType> _itemTypeRepository;

        public ItemService(
            IGenericRepository<Item> itemRepository,
            IGenericRepository<ItemType> itemTypeRepository)
        {
            _itemRepository = itemRepository;
            _itemTypeRepository = itemTypeRepository;
        }
        public async Task<ItemResponse?> CreateAsync(
     CreateItemRequest dto,
     string? imageUrl)
        {
            var itemType = await _itemTypeRepository.GetByIdAsync(dto.ItemTypeId);

            if (itemType == null)
                throw new NotFoundException("Item type not found");

            bool exists = await _itemRepository.AnyAsync(x =>
                x.ItemName == dto.ItemName &&
                x.ItemTypeId == dto.ItemTypeId);

            if (exists)
                throw new ConflictException("Item already exists");

            var item = new Item
            {
                ItemId = Guid.NewGuid(),
                ItemName = dto.ItemName,
                ImgUrl = imageUrl,
                ItemTypeId = dto.ItemTypeId,
                EffectTypeCode = dto.EffectTypeCode,
                EffectValue = dto.EffectValue,
                Description = dto.Description,
                IsActive = true
            };

            await _itemRepository.AddAsync(item);
            await _itemRepository.SaveAsync();

            return new ItemResponse
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                ItemTypeName = itemType.ItemTypeName,
                Image = item.ImgUrl,
                EffectTypeCode = item.EffectTypeCode,
                EffectValue = item.EffectValue,
                IsActive = item.IsActive,
               
            };
        }

        public async Task DeleteAsync(Guid id)
        {
            var item = await _itemRepository.GetByIdAsync(id);

            if (item == null)
                throw new NotFoundException("Item not found");

            item.IsActive = false;
            _itemRepository.Update(item);
            await _itemRepository.SaveAsync();
        }

        public async Task<IEnumerable<ItemResponse>> GetAllAsync()
        {
            var items = await _itemRepository.GetAllAsync();
            var itemTypes = await _itemTypeRepository.GetAllAsync();

            return items.Select(item =>
            {
                var itemType = itemTypes.FirstOrDefault(x =>
                    x.ItemTypeId == item.ItemTypeId);

                return new ItemResponse
                {
                    ItemId = item.ItemId,
                    Image = item.ImgUrl,
                    ItemName = item.ItemName,
                    ItemTypeName = itemType?.ItemTypeName ?? "",
                    EffectTypeCode = item.EffectTypeCode,
                    EffectValue = item.EffectValue,
                    IsActive = item.IsActive
                };
            });
        }

        public async Task<Item?> GetByIdAsync(Guid id)
        {
            return await _itemRepository.GetByIdAsync(id);
        }

        public async Task<ItemResponse> UpdateAsync(
     Guid id,
     UpdateItemRequest dto,
     string? imageUrl)
        {
            var item = await _itemRepository.GetByIdAsync(id);

            if (item == null)
                throw new Exception("Item not found");

            bool exists = await _itemRepository.AnyAsync(x =>
                x.ItemId != id &&
                x.ItemName == dto.ItemName &&
                x.ItemTypeId == dto.ItemTypeId);

            if (exists)
                throw new Exception("Item already exists");

            item.ItemName = dto.ItemName;
            item.ItemTypeId = dto.ItemTypeId;
            item.EffectTypeCode = dto.EffectTypeCode;
            item.EffectValue = dto.EffectValue;
            item.Description = dto.Description;
            item.IsActive = dto.IsActive;

            if (!string.IsNullOrEmpty(imageUrl))
            {
                item.ImgUrl = imageUrl;
            }
            var itemType = await _itemTypeRepository.GetByIdAsync(dto.ItemTypeId);
            _itemRepository.Update(item);
            await _itemRepository.SaveAsync();

            return new ItemResponse
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                ItemTypeName = itemType.ItemTypeName,
                Image = item.ImgUrl,
                EffectTypeCode = item.EffectTypeCode,
                EffectValue = item.EffectValue,
                IsActive = item.IsActive,

            };
        }
    }
}
