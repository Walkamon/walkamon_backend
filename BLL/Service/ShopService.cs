using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace BLL.Service;

public class ShopService : IShopService
{
    private readonly IGenericRepository<ShopItem> _shopItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;
    private readonly IGenericRepository<ItemType> _itemTypeRepository;
    private readonly IGenericRepository<Wallet> _walletRepository;
    private readonly IGenericRepository<InventoryItem> _inventoryRepository;
    private readonly IGenericRepository<ShopPurchase> _shopPurchaseRepository;

    public ShopService(
        IGenericRepository<ShopItem> shopItemRepository,
        IGenericRepository<Item> itemRepository,
        IGenericRepository<ItemType> itemTypeRepository,
        IGenericRepository<Wallet> walletRepository,
        IGenericRepository<InventoryItem> inventoryRepository,
        IGenericRepository<ShopPurchase> shopPurchaseRepository)
    {
        _shopItemRepository = shopItemRepository;
        _itemRepository = itemRepository;
        _itemTypeRepository = itemTypeRepository;
        _walletRepository = walletRepository;
        _inventoryRepository = inventoryRepository;
        _shopPurchaseRepository = shopPurchaseRepository;
    }

    public async Task<List<ShopItemListResponse>> GetShopItemsAsync()
    {
        var shopItems = (await _shopItemRepository.FindAsync(x => x.IsActive))
            .ToList();

        if (shopItems.Count == 0)
        {
            return [];
        }

        var itemIds = shopItems.Select(x => x.ItemId).ToHashSet();
        var items = (await _itemRepository.FindAsync(x =>
                itemIds.Contains(x.ItemId) && x.IsActive))
            .ToDictionary(x => x.ItemId);

        var itemTypeIds = items.Values.Select(x => x.ItemTypeId).ToHashSet();
        var itemTypes = (await _itemTypeRepository.FindAsync(x =>
                itemTypeIds.Contains(x.ItemTypeId)))
            .ToDictionary(x => x.ItemTypeId);

        return shopItems
            .Where(x => items.ContainsKey(x.ItemId))
            .Select(x =>
            {
                var item = items[x.ItemId];
                itemTypes.TryGetValue(item.ItemTypeId, out var itemType);

                return new ShopItemListResponse
                {
                    ShopItemId = x.ShopItemId,
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    ItemTypeName = itemType?.ItemTypeName ?? string.Empty,
                    Image = item.ImgUrl,
                    PriceAmount = x.PriceAmount
                };
            })
            .ToList();
    }

    public async Task<ShopItemDetailResponse> GetShopItemDetailAsync(Guid shopItemId)
    {
        var (shopItem, item) = await GetActiveShopItemWithItemAsync(shopItemId);
        var itemType = await _itemTypeRepository.GetByIdAsync(item.ItemTypeId);

        return new ShopItemDetailResponse
        {
            ShopItemId = shopItem.ShopItemId,
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            ItemTypeName = itemType?.ItemTypeName ?? string.Empty,
            Image = item.ImgUrl,
            PriceAmount = shopItem.PriceAmount,
            EffectTypeCode = item.EffectTypeCode,
            EffectValue = item.EffectValue,
            Description = item.Description
        };
    }

    public async Task<BuyShopItemResponse> BuyShopItemAsync(
        Guid userId,
        BuyShopItemRequest request)
    {
        if (request.Quantity <= 0)
        {
            throw new BadRequestException("Quantity must be greater than 0");
        }

        var (shopItem, item) = await GetActiveShopItemWithItemAsync(request.ShopItemId);
        var wallet = await _walletRepository.GetByIdAsync(userId);

        if (wallet == null)
        {
            throw new NotFoundException("Wallet not found");
        }

        var totalPriceLong = (long)shopItem.PriceAmount * request.Quantity;
        if (totalPriceLong > int.MaxValue)
        {
            throw new BadRequestException("Total price amount is too large");
        }

        var totalPriceAmount = (int)totalPriceLong;

        if (wallet.Balance < totalPriceAmount)
        {
            throw new BadRequestException("Insufficient wallet balance");
        }

        var inventoryItem = await _inventoryRepository.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.ItemId == shopItem.ItemId);

        if (inventoryItem == null)
        {
            inventoryItem = new InventoryItem
            {
                UserId = userId,
                ItemId = shopItem.ItemId,
                Quantity = request.Quantity
            };

            await _inventoryRepository.AddAsync(inventoryItem);
        }
        else
        {
            if (inventoryItem.Quantity > int.MaxValue - request.Quantity)
            {
                throw new BadRequestException("Inventory quantity is too large");
            }

            inventoryItem.Quantity += request.Quantity;
            _inventoryRepository.Update(inventoryItem);
        }

        wallet.Balance -= totalPriceAmount;
        _walletRepository.Update(wallet);

        var purchase = new ShopPurchase
        {
            PurchaseId = Guid.NewGuid(),
            UserId = userId,
            ShopItemId = shopItem.ShopItemId,
            Quantity = request.Quantity,
            UnitPriceAmount = shopItem.PriceAmount,
            PurchasedAt = DateTime.UtcNow
        };

        await _shopPurchaseRepository.AddAsync(purchase);
        await _shopPurchaseRepository.SaveAsync();

        return new BuyShopItemResponse
        {
            ShopItemId = shopItem.ShopItemId,
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            Quantity = request.Quantity,
            UnitPriceAmount = shopItem.PriceAmount,
            TotalPriceAmount = totalPriceAmount,
            WalletBalance = wallet.Balance,
            InventoryQuantity = inventoryItem.Quantity
        };
    }

    private async Task<(ShopItem ShopItem, Item Item)> GetActiveShopItemWithItemAsync(
        Guid shopItemId)
    {
        var shopItem = await _shopItemRepository.GetByIdAsync(shopItemId);

        if (shopItem == null || !shopItem.IsActive)
        {
            throw new NotFoundException("Shop item not found");
        }

        var item = await _itemRepository.GetByIdAsync(shopItem.ItemId);

        if (item == null || !item.IsActive)
        {
            throw new NotFoundException("Shop item not found");
        }

        return (shopItem, item);
    }
}
