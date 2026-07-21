using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace BLL.Service;

public class InventoryService : IInventoryService
{
    private readonly IGenericRepository<InventoryItem> _inventoryRepository;
    private readonly IGenericRepository<Item> _itemRepository;
    private readonly IGenericRepository<ItemType> _itemTypeRepository;
    private readonly IGenericRepository<UserPet> _userPetRepository;
    private readonly IAchievementProgressService _achievementProgressService;
    private readonly IMissionProgressService _missionProgressService;

    public InventoryService(
        IGenericRepository<InventoryItem> inventoryRepository,
        IGenericRepository<Item> itemRepository,
        IGenericRepository<ItemType> itemTypeRepository,
        IGenericRepository<UserPet> userPetRepository,
        IAchievementProgressService achievementProgressService,
        IMissionProgressService missionProgressService)
    {
        _inventoryRepository = inventoryRepository;
        _itemRepository = itemRepository;
        _itemTypeRepository = itemTypeRepository;
        _userPetRepository = userPetRepository;
        _achievementProgressService = achievementProgressService;
        _missionProgressService = missionProgressService;
    }

    public async Task<List<InventoryItemResponse>> GetInventoryAsync(Guid userId)
    {
        var inventoryItems = (await _inventoryRepository.FindAsync(x =>
                x.UserId == userId && x.Quantity > 0))
            .ToList();

        if (inventoryItems.Count == 0)
        {
            return [];
        }

        var itemIds = inventoryItems.Select(x => x.ItemId).ToHashSet();
        var items = (await _itemRepository.FindAsync(x =>
                itemIds.Contains(x.ItemId) && x.IsActive))
            .ToDictionary(x => x.ItemId);

        var itemTypeIds = items.Values.Select(x => x.ItemTypeId).ToHashSet();
        var itemTypes = (await _itemTypeRepository.FindAsync(x =>
                itemTypeIds.Contains(x.ItemTypeId)))
            .ToDictionary(x => x.ItemTypeId);

        return inventoryItems
            .Where(x => items.ContainsKey(x.ItemId))
            .Select(x => ToInventoryItemResponse(
                x,
                items[x.ItemId],
                itemTypes))
            .ToList();
    }

    public async Task<InventoryItemResponse> GetInventoryItemDetailAsync(
        Guid userId,
        Guid itemId)
    {
        var inventoryItem = await GetOwnedInventoryItemAsync(userId, itemId);
        var item = await GetActiveItemAsync(itemId);
        var itemType = await _itemTypeRepository.GetByIdAsync(item.ItemTypeId);

        return ToInventoryItemResponse(
            inventoryItem,
            item,
            itemType == null
                ? []
                : new Dictionary<Guid, ItemType> { [itemType.ItemTypeId] = itemType });
    }

    public async Task<UseItemResponse> UseItemAsync(
        Guid userId,
        UseItemRequest request)
    {
        var inventoryItem = await GetOwnedInventoryItemAsync(
            userId,
            request.ItemId);
        var item = await GetActiveItemAsync(request.ItemId);

        if (string.IsNullOrWhiteSpace(item.EffectTypeCode)
            || !item.EffectValue.HasValue)
        {
            throw new BadRequestException("Item effect is not configured");
        }

        var effectTypeCode = item.EffectTypeCode.Trim().ToLowerInvariant();
        var effectValue = item.EffectValue.Value;

        if (effectTypeCode.StartsWith("pvp_", StringComparison.Ordinal))
        {
            throw new ConflictException("PvP items can only be used through an active Sprint match.");
        }

        var userPet = await _userPetRepository.GetByIdAsync(userId);
        if (userPet == null)
        {
            throw new NotFoundException("Pet not found");
        }

        ApplyItemEffect(userPet, effectTypeCode, effectValue);

        inventoryItem.Quantity--;

        if (inventoryItem.Quantity == 0)
        {
            _inventoryRepository.Delete(inventoryItem);
        }
        else
        {
            _inventoryRepository.Update(inventoryItem);
        }

        _userPetRepository.Update(userPet);
        await _inventoryRepository.SaveAsync();

        await _achievementProgressService.AddProgressAsync(userId, "feed_pet", 1);
        await _missionProgressService.AddProgressAsync(userId, "feed_pet", 1);

        return new UseItemResponse
        {
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            EffectTypeCode = effectTypeCode,
            EffectValue = effectValue,
            RemainingQuantity = inventoryItem.Quantity,
            LifeForce = userPet.CurrentPetLifeForce,
            Energy = userPet.CurrentPetEnergy,
            Bond = userPet.CurrentPetBond
        };
    }

    private async Task<InventoryItem> GetOwnedInventoryItemAsync(
        Guid userId,
        Guid itemId)
    {
        var inventoryItem = await _inventoryRepository.FirstOrDefaultAsync(x =>
            x.UserId == userId
            && x.ItemId == itemId
            && x.Quantity > 0);

        if (inventoryItem == null)
        {
            throw new NotFoundException("Item not found in inventory");
        }

        return inventoryItem;
    }

    private async Task<Item> GetActiveItemAsync(Guid itemId)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);

        if (item == null || !item.IsActive)
        {
            throw new NotFoundException("Item not found in inventory");
        }

        return item;
    }

    internal static void ApplyItemEffect(
        UserPet userPet,
        string effectTypeCode,
        int effectValue)
    {
        if (effectValue < 0)
        {
            throw new BadRequestException("Item effect value cannot be negative");
        }

        switch (effectTypeCode)
        {
            case "life_force":
            case "sml":
                userPet.CurrentPetLifeForce = RestoreUpToMaximum(
                    userPet.CurrentPetLifeForce,
                    userPet.PetLifeForce,
                    effectValue);
                break;
            case "energy":
                userPet.CurrentPetEnergy = RestoreUpToMaximum(
                    userPet.CurrentPetEnergy,
                    userPet.PetEnergy,
                    effectValue);
                break;
            case "bond":
                userPet.CurrentPetBond = RestoreUpToMaximum(
                    userPet.CurrentPetBond,
                    userPet.PetBond,
                    effectValue);
                break;
            default:
                throw new BadRequestException("Unsupported item effect type");
        }
    }

    private static int RestoreUpToMaximum(int current, int maximum, int amount)
    {
        if (maximum < 0)
        {
            throw new BadRequestException("Pet maximum stat is configured incorrectly");
        }

        return (int)Math.Min(maximum, (long)Math.Max(0, current) + amount);
    }

    private static InventoryItemResponse ToInventoryItemResponse(
        InventoryItem inventoryItem,
        Item item,
        IReadOnlyDictionary<Guid, ItemType> itemTypes)
    {
        itemTypes.TryGetValue(item.ItemTypeId, out var itemType);

        return new InventoryItemResponse
        {
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            ItemTypeName = itemType?.ItemTypeName ?? string.Empty,
            Image = item.ImgUrl,
            EffectTypeCode = item.EffectTypeCode,
            EffectValue = item.EffectValue,
            Description = item.Description,
            Quantity = inventoryItem.Quantity
        };
    }
}
