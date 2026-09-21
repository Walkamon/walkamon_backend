using BLL.Exceptions;
using BLL.Interfaces;
using DAL.Data;
using DAL.DTO;
using DAL.Extensions;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BLL.Service;

public class ShopService : IShopService
{
    private readonly WalkamonContext _context;
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
        IGenericRepository<ShopPurchase> shopPurchaseRepository,
        WalkamonContext context)
    {
        _context = context;
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
                    PriceAmount = x.PriceAmount,
                    IsActive = item.IsActive && x.IsActive,
                    EffectTypeCode = item.EffectTypeCode,
                    EffectValue = item.EffectValue,
                    Description = item.Description,
                    UsageContextCode = ResolveUsageContext(item.EffectTypeCode),
                    CanUseNow = ResolveUsageContext(item.EffectTypeCode) == "home_pet",
                    CanEquipForPvp = ResolveUsageContext(item.EffectTypeCode) == "pvp_loadout"
                    ,ItemNameVi = item.ItemNameVi
                    ,ItemNameEn = item.ItemNameEn
                    ,DescriptionVi = item.DescriptionVi
                    ,DescriptionEn = item.DescriptionEn
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
            Description = item.Description,
            IsActive = shopItem.IsActive && item.IsActive,
            UsageContextCode = ResolveUsageContext(item.EffectTypeCode),
            CanUseNow = ResolveUsageContext(item.EffectTypeCode) == "home_pet",
            CanEquipForPvp = ResolveUsageContext(item.EffectTypeCode) == "pvp_loadout"
            ,ItemNameVi = item.ItemNameVi
            ,ItemNameEn = item.ItemNameEn
            ,DescriptionVi = item.DescriptionVi
            ,DescriptionEn = item.DescriptionEn
        };
    }

    public async Task<BuyShopItemResponse> BuyShopItemAsync(
        Guid userId,
        BuyShopItemRequest request)
    {
        if (request.Quantity <= 0 || request.ShopItemId == Guid.Empty || request.RequestId == Guid.Empty)
        {
            throw new BadRequestException("A valid item, positive quantity and non-empty request ID are required.");
        }

        var purchaseId = request.RequestId ?? Guid.NewGuid();
        return await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var wallet = await _context.Wallets
                .FromSqlInterpolated($"SELECT * FROM wallets WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {userId}")
                .SingleOrDefaultAsync();

            if (wallet == null)
            {
                throw new NotFoundException("Wallet not found");
            }

            // The existing purchase primary key is also the retry key; no schema change.
            var previous = await _context.ShopPurchases.AsNoTracking()
                .SingleOrDefaultAsync(x => x.PurchaseId == purchaseId);
            if (previous != null)
            {
                if (previous.UserId != userId || previous.ShopItemId != request.ShopItemId ||
                    previous.Quantity != request.Quantity)
                    throw new ConflictException("Request ID has already been used for another purchase.");
                var previousShopItem = await _shopItemRepository.GetByIdAsync(previous.ShopItemId);
                var previousItem = await _itemRepository.GetByIdAsync(previousShopItem.ItemId);
                var quantity = await _context.InventoryItems.AsNoTracking()
                    .Where(x => x.UserId == userId && x.ItemId == previousItem.ItemId)
                    .Select(x => x.Quantity).SingleOrDefaultAsync();
                return new BuyShopItemResponse
                {
                    PurchaseId = previous.PurchaseId,
                    ShopItemId = previous.ShopItemId,
                    ItemId = previousItem.ItemId,
                    ItemName = previousItem.ItemName,
                    Quantity = previous.Quantity,
                    UnitPriceAmount = previous.UnitPriceAmount,
                    TotalPriceAmount = checked(previous.UnitPriceAmount * previous.Quantity),
                    WalletBalance = wallet.Balance,
                    InventoryQuantity = quantity
                };
            }

            var (shopItem, item) = await GetActiveShopItemWithItemAsync(request.ShopItemId);
            if (shopItem.PriceAmount < 0)
                throw new ConflictException("Shop item price is invalid.");
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

            var inventoryItem = await _context.InventoryItems
                .FromSqlInterpolated($"SELECT * FROM inventory_items WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {userId} AND item_id = {shopItem.ItemId}")
                .SingleOrDefaultAsync();

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
                PurchaseId = purchaseId,
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
                PurchaseId = purchaseId,
                ShopItemId = shopItem.ShopItemId,
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                Quantity = request.Quantity,
                UnitPriceAmount = shopItem.PriceAmount,
                TotalPriceAmount = totalPriceAmount,
                WalletBalance = wallet.Balance,
                InventoryQuantity = inventoryItem.Quantity
            };
        });
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

    private static string ResolveUsageContext(string? effectTypeCode)
    {
        if (string.IsNullOrWhiteSpace(effectTypeCode)) return "none";
        return effectTypeCode.StartsWith("pvp_", StringComparison.OrdinalIgnoreCase)
            ? "pvp_loadout"
            : "home_pet";
    }
}
