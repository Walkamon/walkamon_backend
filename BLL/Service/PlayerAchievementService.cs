using BLL.Exceptions;
using BLL.Interfaces;
using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BLL.Service;

public class PlayerAchievementService : IPlayerAchievementService
{
    private readonly IGenericRepository<Achievement> _achievementRepository;
    private readonly IGenericRepository<AchievementCondition> _achievementConditionRepository;
    private readonly IGenericRepository<UserAchievement> _userAchievementRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;
    private readonly IGenericRepository<Wallet> _walletRepository;
    private readonly IGenericRepository<InventoryItem> _inventoryRepository;
    private readonly WalkamonContext _context;
    private readonly IAchievementProgressService _achievementProgressService;

    public PlayerAchievementService(
        IGenericRepository<Achievement> achievementRepository,
        IGenericRepository<AchievementCondition> achievementConditionRepository,
        IGenericRepository<UserAchievement> userAchievementRepository,
        IGenericRepository<RewardPackage> rewardPackageRepository,
        IGenericRepository<RewardPackageItem> rewardPackageItemRepository,
        IGenericRepository<Item> itemRepository,
        IGenericRepository<Wallet> walletRepository,
        IGenericRepository<InventoryItem> inventoryRepository,
        WalkamonContext context,
        IAchievementProgressService achievementProgressService)
    {
        _achievementRepository = achievementRepository;
        _achievementConditionRepository = achievementConditionRepository;
        _userAchievementRepository = userAchievementRepository;
        _rewardPackageRepository = rewardPackageRepository;
        _rewardPackageItemRepository = rewardPackageItemRepository;
        _itemRepository = itemRepository;
        _walletRepository = walletRepository;
        _inventoryRepository = inventoryRepository;
        _context = context;
        _achievementProgressService = achievementProgressService;
    }

    public async Task<List<PlayerAchievementItemResponse>> GetAchievementsAsync(
        Guid userId)
    {
        var achievements = (await _achievementRepository.FindAsync(x => x.IsActive))
            .OrderBy(x => x.Title)
            .ToList();

        if (achievements.Count == 0)
        {
            return [];
        }

        var achievementIds = achievements
            .Select(x => x.AchievementId)
            .ToHashSet();

        // Load reward data
        var rewardPackageIds = achievements
            .Select(x => x.RewardPackageId)
            .ToHashSet();
        var rewardPackages = (await _rewardPackageRepository.FindAsync(x =>
                rewardPackageIds.Contains(x.RewardPackageId)))
            .ToDictionary(x => x.RewardPackageId);
        var rewardItems = (await _rewardPackageItemRepository.FindAsync(x =>
                rewardPackageIds.Contains(x.RewardPackageId)))
            .ToList();
        var rewardItemIds = rewardItems
            .Select(x => x.ItemId)
            .ToHashSet();
        var items = rewardItemIds.Count == 0
            ? new Dictionary<Guid, Item>()
            : (await _itemRepository.FindAsync(x =>
                    rewardItemIds.Contains(x.ItemId)))
                .ToDictionary(x => x.ItemId);

        // Load user progress
        var userAchievements = (await _userAchievementRepository.FindAsync(x =>
                x.UserId == userId
                && achievementIds.Contains(x.AchievementId)))
            .ToDictionary(x => x.AchievementId);

        // Load assignment conditions to check prerequisites
        var assignmentConditions = (await _achievementConditionRepository.FindAsync(x =>
                achievementIds.Contains(x.AchievementId)
                && x.ConditionGroup == "assignment"))
            .GroupBy(x => x.AchievementId)
            .ToDictionary(x => x.Key, x => x.ToList());

        return achievements
            .Select(x => ToAchievementItemResponse(
                x,
                rewardPackages,
                rewardItems,
                items,
                userAchievements,
                assignmentConditions))
            .ToList();
    }

    public async Task<PlayerAchievementItemResponse> GetAchievementDetailAsync(Guid userId, Guid achievementId)
    {
        var achievement = await _achievementRepository.GetByIdAsync(achievementId);
        if (achievement == null || !achievement.IsActive)
        {
            throw new NotFoundException("Achievement not found");
        }

        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(achievement.RewardPackageId);
        var rewardItems = (await _rewardPackageItemRepository.FindAsync(x => x.RewardPackageId == achievement.RewardPackageId)).ToList();
        
        var itemIds = rewardItems.Select(x => x.ItemId).ToHashSet();
        var items = itemIds.Count > 0 
            ? (await _itemRepository.FindAsync(x => itemIds.Contains(x.ItemId))).ToDictionary(x => x.ItemId) 
            : new Dictionary<Guid, Item>();
            
        var userAchievement = await _userAchievementRepository.FirstOrDefaultAsync(x => x.UserId == userId && x.AchievementId == achievementId);
        var conditions = (await _achievementConditionRepository.FindAsync(x => x.AchievementId == achievementId && x.ConditionGroup == "assignment")).ToList();

        var rewardPackagesDict = rewardPackage != null 
            ? new Dictionary<Guid, RewardPackage> { [rewardPackage.RewardPackageId] = rewardPackage } 
            : new Dictionary<Guid, RewardPackage>();
            
        var userAchievementsDict = userAchievement != null 
            ? new Dictionary<Guid, UserAchievement> { [userAchievement.AchievementId] = userAchievement } 
            : new Dictionary<Guid, UserAchievement>();
            
        var assignmentConditionsDict = new Dictionary<Guid, List<AchievementCondition>> 
        { 
            [achievementId] = conditions 
        };

        // We also need to fetch referenced user achievements to check prerequisites properly.
        if (conditions.Count > 0)
        {
            var refIds = conditions.Where(c => c.ReferenceAchievementId.HasValue).Select(c => c.ReferenceAchievementId!.Value).ToHashSet();
            if (refIds.Count > 0)
            {
                var refUserAchievements = await _userAchievementRepository.FindAsync(x => x.UserId == userId && refIds.Contains(x.AchievementId));
                foreach (var rua in refUserAchievements)
                {
                    userAchievementsDict[rua.AchievementId] = rua;
                }
            }
        }

        return ToAchievementItemResponse(
            achievement,
            rewardPackagesDict,
            rewardItems,
            items,
            userAchievementsDict,
            assignmentConditionsDict);
    }

    public async Task<ClaimAchievementRewardResponse> ClaimAchievementRewardAsync(
        Guid userId,
        Guid achievementId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var achievement = await _achievementRepository.FirstOrDefaultAsync(x =>
                x.AchievementId == achievementId
                && x.IsActive);

            if (achievement == null)
            {
                throw new NotFoundException("Achievement not found");
            }

            var userAchievement = await _context.UserAchievements
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM user_achievements WITH (UPDLOCK, HOLDLOCK)
                    WHERE user_id = {{userId}}
                      AND achievement_id = {{achievementId}}
                    """)
                .SingleOrDefaultAsync();

            if (userAchievement == null)
            {
                throw new BadRequestException("Achievement is not unlocked");
            }

            if (userAchievement.ClaimedAt.HasValue)
            {
                throw new BadRequestException("Achievement reward already claimed");
            }

            if (!userAchievement.UnlockedAt.HasValue)
            {
                throw new BadRequestException("Achievement is not unlocked");
            }

            if (userAchievement.ProgressValue < achievement.TargetValue)
            {
                throw new BadRequestException("Achievement is not completed");
            }

            var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
                achievement.RewardPackageId);

            if (rewardPackage == null)
            {
                throw new NotFoundException("Reward package not found");
            }

            var wallet = await _context.Wallets
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM wallets WITH (UPDLOCK, HOLDLOCK)
                    WHERE user_id = {{userId}}
                    """)
                .SingleOrDefaultAsync();

            if (wallet == null)
            {
                throw new NotFoundException("Wallet not found");
            }

            if ((long)wallet.Balance + rewardPackage.WalletAmount > int.MaxValue)
            {
                throw new BadRequestException("Wallet balance is too large");
            }

            var rewardItems = (await _rewardPackageItemRepository.FindAsync(x =>
                    x.RewardPackageId == rewardPackage.RewardPackageId))
                .ToList();
            var rewardItemIds = rewardItems.Select(x => x.ItemId).ToHashSet();
            var itemsDict = rewardItemIds.Count == 0
                ? new Dictionary<Guid, Item>()
                : (await _itemRepository.FindAsync(x =>
                        rewardItemIds.Contains(x.ItemId)))
                    .ToDictionary(x => x.ItemId);
            var inventoryItems = rewardItemIds.Count == 0
                ? new Dictionary<Guid, InventoryItem>()
                : (await _inventoryRepository.FindAsync(x =>
                        x.UserId == userId && rewardItemIds.Contains(x.ItemId)))
                    .ToDictionary(x => x.ItemId);

            foreach (var rewardItem in rewardItems)
            {
                if (inventoryItems.TryGetValue(rewardItem.ItemId, out var inventoryItem)
                    && (long)inventoryItem.Quantity + rewardItem.Quantity > int.MaxValue)
                {
                    throw new BadRequestException("Inventory quantity is too large");
                }
            }

            wallet.Balance += rewardPackage.WalletAmount;
            _walletRepository.Update(wallet);

            foreach (var rewardItem in rewardItems)
            {
                if (inventoryItems.TryGetValue(rewardItem.ItemId, out var inventoryItem))
                {
                    inventoryItem.Quantity += rewardItem.Quantity;
                    _inventoryRepository.Update(inventoryItem);
                    continue;
                }

                inventoryItem = new InventoryItem
                {
                    UserId = userId,
                    ItemId = rewardItem.ItemId,
                    Quantity = rewardItem.Quantity
                };
                await _inventoryRepository.AddAsync(inventoryItem);
                inventoryItems[rewardItem.ItemId] = inventoryItem;
            }

            var claimedAt = DateTime.UtcNow;
            userAchievement.ClaimedAt = claimedAt;
            _userAchievementRepository.Update(userAchievement);

            await _userAchievementRepository.SaveAsync();
            await transaction.CommitAsync();

            if (rewardPackage.WalletAmount > 0)
            {
                await _achievementProgressService.AddProgressAsync(userId, "wallet_earned", rewardPackage.WalletAmount);
            }

            return new ClaimAchievementRewardResponse
            {
                AchievementId = achievement.AchievementId,
                WalletAmount = rewardPackage.WalletAmount,
                RewardItems = ToRewardItemResponses(rewardItems, itemsDict),
                WalletBalance = wallet.Balance,
                ClaimedAt = claimedAt
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static PlayerAchievementItemResponse ToAchievementItemResponse(
        Achievement achievement,
        IReadOnlyDictionary<Guid, RewardPackage> rewardPackages,
        IReadOnlyCollection<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyDictionary<Guid, UserAchievement> userAchievements,
        IReadOnlyDictionary<Guid, List<AchievementCondition>> assignmentConditions)
    {
        rewardPackages.TryGetValue(achievement.RewardPackageId, out var rewardPackage);
        userAchievements.TryGetValue(achievement.AchievementId, out var userAchievement);

        var achievementRewardItems = rewardItems
            .Where(x => x.RewardPackageId == achievement.RewardPackageId)
            .ToList();
        var progressValue = userAchievement?.ProgressValue ?? 0;
        var isUnlocked = userAchievement?.UnlockedAt.HasValue == true;
        var isClaimed = userAchievement?.ClaimedAt.HasValue == true;

        // Check if assignment conditions (prerequisites) are met
        var hasPrerequisites = assignmentConditions.TryGetValue(
            achievement.AchievementId, out var conditions) && conditions.Count > 0;
        var prerequisitesMet = true;

        if (hasPrerequisites)
        {
            prerequisitesMet = conditions!.All(c =>
            {
                if (!c.ReferenceAchievementId.HasValue)
                {
                    return true;
                }

                return userAchievements.TryGetValue(
                    c.ReferenceAchievementId.Value,
                    out var refUserAchievement)
                    && refUserAchievement.UnlockedAt.HasValue;
            });
        }

        var canClaim = isUnlocked
            && !isClaimed
            && progressValue >= achievement.TargetValue
            && prerequisitesMet;

        return new PlayerAchievementItemResponse
        {
            AchievementId = achievement.AchievementId,
            Title = achievement.Title,
            IconUrl = achievement.IconUrl,
            MetricCode = achievement.MetricCode,
            ProgressValue = progressValue,
            TargetValue = achievement.TargetValue,
            WalletAmount = rewardPackage?.WalletAmount ?? 0,
            RewardItems = ToRewardItemResponses(achievementRewardItems, items),
            IsUnlocked = isUnlocked,
            CanClaim = canClaim,
            UnlockedAt = userAchievement?.UnlockedAt,
            ClaimedAt = userAchievement?.ClaimedAt
        };
    }

    private static List<PlayerAchievementRewardItemResponse> ToRewardItemResponses(
        IReadOnlyCollection<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items)
    {
        return rewardItems
            .Where(x => items.ContainsKey(x.ItemId))
            .Select(x => new PlayerAchievementRewardItemResponse
            {
                ItemId = x.ItemId,
                ItemName = items[x.ItemId].ItemName,
                Quantity = x.Quantity
            })
            .ToList();
    }
}
