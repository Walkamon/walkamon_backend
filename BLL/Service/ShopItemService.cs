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
    public class ShopItemService : IShopItemService
    {
        private readonly IGenericRepository<ShopItem> _shopItemRepo;
        private readonly IGenericRepository<Item> _itemRepo;
        private readonly IShopItemRepository _shopItemRepository;
        public ShopItemService(
            IGenericRepository<ShopItem> shopItemRepo,
            IGenericRepository<Item> itemRepo,
               IShopItemRepository shopItemRepository)
        {
            _shopItemRepo = shopItemRepo;
            _itemRepo = itemRepo;
            _shopItemRepository = shopItemRepository;
        }

        public async Task<List<ShopItemResponse>> GetAllAsync()
        {
            var data = await _shopItemRepository.GetAllWithItemAsync();

            
            return data.Select(x => new ShopItemResponse
            {
                ShopItemId = x.ShopItemId,
                ItemName = x.Item.ItemName,
                ItemQuantity = x.ItemQuantity,
                PriceAmount = x.PriceAmount,
                IsActive = x.IsActive
            }).ToList();
        }

        public async Task<ShopItemResponse?> GetByIdAsync(Guid id)
        {
            var entity = await _shopItemRepo.GetByIdAsync(id);

            if (entity == null)
                return null;
            var item = (await _itemRepo.GetAllAsync())
               .FirstOrDefault(x =>
                   x.ItemId == entity.ItemId &&
                   x.IsActive);
            return new ShopItemResponse
            {
                ShopItemId = entity.ShopItemId,
                ItemName = item.ItemName,
                ItemQuantity = entity.ItemQuantity,
                PriceAmount = entity.PriceAmount,
                IsActive = entity.IsActive
            };
        }

        public async Task<ShopItemResponse> CreateAsync(ShopItemRequest request)
        {
           
            var item = (await _itemRepo.GetAllAsync())
                .FirstOrDefault(x =>
                    x.ItemId == request.ItemId &&
                    x.IsActive);

            if (item == null)
            {
                throw new NotFoundException("Item does not exist or is inactive.");
            }

          
            var existingShopItem = (await _shopItemRepo.GetAllAsync())
                .FirstOrDefault(x =>
                    x.ItemId == request.ItemId);

            if (existingShopItem != null)
            {
               
                existingShopItem.ItemQuantity = request.ItemQuantity;
                existingShopItem.PriceAmount = request.PriceAmount;
                existingShopItem.IsActive = true;

                _shopItemRepo.Update(existingShopItem);
                await _shopItemRepo.SaveAsync();

                return new ShopItemResponse
                {
                    ShopItemId = existingShopItem.ShopItemId,
                    ItemName = item.ItemName,
                    ItemQuantity = existingShopItem.ItemQuantity,
                    PriceAmount = existingShopItem.PriceAmount,
                    IsActive = existingShopItem.IsActive
                };
            }

         
            var entity = new ShopItem
            {
                ShopItemId = Guid.NewGuid(),
                ItemId = request.ItemId,
                ItemQuantity = request.ItemQuantity,
                PriceAmount = request.PriceAmount,
                IsActive = true
            };

            await _shopItemRepo.AddAsync(entity);
            await _shopItemRepo.SaveAsync();

            return new ShopItemResponse
            {
                ShopItemId = entity.ShopItemId,
                ItemName = item.ItemName,
                ItemQuantity = entity.ItemQuantity,
                PriceAmount = entity.PriceAmount,
                IsActive = entity.IsActive
            };
        }

        public async Task<ShopItemResponse> UpdateAsync(Guid id, ShopItemRequest request)
        {
            var entity = await _shopItemRepo.GetByIdAsync(id);

            if (entity == null)
            {
                throw new NotFoundException("ShopItem not found.");
            }

            var item = (await _itemRepo.GetAllAsync())
                .FirstOrDefault(x =>
                    x.ItemId == request.ItemId &&
                    x.IsActive);

            if (item == null)
            {
                throw new NotFoundException("Item does not exist or is inactive.");
            }

            entity.ItemId = request.ItemId;
            entity.ItemQuantity = request.ItemQuantity;
            entity.PriceAmount = request.PriceAmount;

            entity.ItemId = request.ItemId;
            entity.ItemQuantity = request.ItemQuantity;
            entity.PriceAmount = request.PriceAmount;

            _shopItemRepo.Update(entity);

            return new ShopItemResponse
            {
                ShopItemId = entity.ShopItemId,
                ItemName = item.ItemName ,
                ItemQuantity = entity.ItemQuantity,
                PriceAmount = entity.PriceAmount,
                IsActive = entity.IsActive
            };
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _shopItemRepo.GetByIdAsync(id);

            if (entity == null)
                return false;

            entity.IsActive = false;

            _shopItemRepo.Update(entity);

            await _shopItemRepo.SaveAsync();

            return true;
        }
    }
}
