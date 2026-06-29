using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace BLL.Service;

public class AdminAchievementService : IAdminAchievementService
{
    private const string CompletionConditionGroup = "completion";
    private const string AssignmentConditionGroup = "assignment";

    private readonly IGenericRepository<Achievement> _achievementRepository;
    private readonly IGenericRepository<AchievementCondition> _achievementConditionRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;
    private readonly IGenericRepository<UserAchievement> _userAchievementRepository;

    public AdminAchievementService(
        IGenericRepository<Achievement> achievementRepository,
        IGenericRepository<AchievementCondition> achievementConditionRepository,
        IGenericRepository<RewardPackage> rewardPackageRepository,
        IGenericRepository<RewardPackageItem> rewardPackageItemRepository,
        IGenericRepository<Item> itemRepository,
        IGenericRepository<UserAchievement> userAchievementRepository)
    {
        _achievementRepository = achievementRepository;
        _achievementConditionRepository = achievementConditionRepository;
        _rewardPackageRepository = rewardPackageRepository;
        _rewardPackageItemRepository = rewardPackageItemRepository;
        _itemRepository = itemRepository;
        _userAchievementRepository = userAchievementRepository;
    }

    public async Task<AdminAchievementListResponse> GetAchievementsAsync()
    {
        var achievements = (await _achievementRepository.GetAllAsync())
            .OrderBy(x => x.Title)
            .ToList();

        var achievementIds = achievements
            .Select(x => x.AchievementId)
            .ToArray();

        var rewardPackageIds = achievements
            .Select(x => x.RewardPackageId)
            .Distinct()
            .ToArray();
        var rewardPackages = (await _rewardPackageRepository.FindAsync(x =>
                rewardPackageIds.Contains(x.RewardPackageId)))
            .ToDictionary(x => x.RewardPackageId);
        var rewardItems = (await _rewardPackageItemRepository.FindAsync(x =>
                rewardPackageIds.Contains(x.RewardPackageId)))
            .ToList();
        var conditions = (await _achievementConditionRepository.FindAsync(x =>
                achievementIds.Contains(x.AchievementId)))
            .ToList();
        var userAchievements = (await _userAchievementRepository.FindAsync(x => 
                achievementIds.Contains(x.AchievementId)))
            .ToList();
        var items = await GetItemsByRewardItemsAsync(rewardItems);

        var totalUnlocks = userAchievements.Count(x => x.UnlockedAt != null);
        var completionRates = achievements.Select(ach => 
        {
            var usersWithProgress = userAchievements.Where(ua => ua.AchievementId == ach.AchievementId).ToList();
            if (usersWithProgress.Count == 0) return 0.0;
            var unlockedUsers = usersWithProgress.Count(ua => ua.UnlockedAt != null);
            return (double)unlockedUsers / usersWithProgress.Count * 100;
        }).ToList();
        var averageCompletionRate = completionRates.Count > 0 ? Math.Round(completionRates.Average(), 1) : 0.0;

        return new AdminAchievementListResponse
        {
            Summary = new AdminAchievementSummaryResponse
            {
                TotalAchievements = achievements.Count,
                ActiveAchievements = achievements.Count(x => x.IsActive),
                TotalUnlocks = totalUnlocks,
                AverageCompletionRate = averageCompletionRate
            },
            Achievements = achievements
                .Select(x => ToListItemResponse(
                    x,
                    rewardPackages,
                    rewardItems,
                    items,
                    conditions))
                .ToList()
        };
    }

    public async Task<AdminAchievementDetailResponse> GetAchievementDetailAsync(
        Guid achievementId)
    {
        var achievement = await GetAchievementOrThrowAsync(achievementId);

        return await ToDetailResponseAsync(achievement);
    }

    public async Task<AdminAchievementDetailResponse> CreateAchievementAsync(
        CreateAdminAchievementRequest request, string? iconUrl)
    {
        await EnsureValidRequestAsync(request);
        var assignmentConditions = NormalizeConditions(
            request.AssignmentConditions);

        var rewardPackage = new RewardPackage
        {
            RewardPackageId = Guid.NewGuid(),
            PackageName = $"achievement-{Guid.NewGuid():N}",
            WalletAmount = request.WalletAmount
        };
        var achievement = new Achievement
        {
            AchievementId = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            MetricCode = MissionMetricCodeCatalog.NormalizeOrThrow(request.MetricCode),
            TargetValue = request.TargetValue,
            IconUrl = iconUrl,
            RewardPackageId = rewardPackage.RewardPackageId,
            IsActive = request.IsActive
        };

        await _rewardPackageRepository.AddAsync(rewardPackage);
        await _achievementRepository.AddAsync(achievement);
        await AddRewardItemsAsync(
            rewardPackage.RewardPackageId,
            request.RewardItems);
        await AddConditionsAsync(
            achievement.AchievementId,
            assignmentConditions);
        await _achievementRepository.SaveAsync();

        return await ToDetailResponseAsync(achievement);
    }

    public async Task<AdminAchievementDetailResponse> UpdateAchievementAsync(
        Guid achievementId,
        UpdateAdminAchievementRequest request,
        string? iconUrl)
    {
        await EnsureValidUpdateRequestAsync(request);

        var achievement = await GetAchievementOrThrowAsync(achievementId);
        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
            achievement.RewardPackageId);

        if (rewardPackage == null)
        {
            throw new NotFoundException("Reward package not found");
        }

        var assignmentConditions = NormalizeConditions(
            request.AssignmentConditions);

        achievement.Title = request.Title.Trim();
        achievement.Description = request.Description?.Trim();
        achievement.MetricCode = MissionMetricCodeCatalog.NormalizeOrThrow(request.MetricCode);
        achievement.TargetValue = request.TargetValue;
        achievement.IsActive = request.IsActive;

        if (iconUrl != null)
        {
            achievement.IconUrl = iconUrl;
        }

        rewardPackage.WalletAmount = request.WalletAmount;

        _achievementRepository.Update(achievement);
        _rewardPackageRepository.Update(rewardPackage);
        await ReplaceRewardItemsAsync(
            achievement.RewardPackageId,
            request.RewardItems);
        await ReplaceConditionsAsync(
            achievement.AchievementId,
            assignmentConditions);
        await _achievementRepository.SaveAsync();

        return await ToDetailResponseAsync(achievement);
    }

    public async Task UpdateAchievementStatusAsync(
        Guid achievementId,
        UpdateAdminAchievementStatusRequest request)
    {
        var achievement = await GetAchievementOrThrowAsync(achievementId);

        achievement.IsActive = request.IsActive;
        _achievementRepository.Update(achievement);
        await _achievementRepository.SaveAsync();
    }

    private async Task<Achievement> GetAchievementOrThrowAsync(Guid achievementId)
    {
        var achievement = await _achievementRepository.GetByIdAsync(achievementId);

        if (achievement == null)
        {
            throw new NotFoundException("Achievement not found");
        }

        return achievement;
    }

    private async Task<AdminAchievementDetailResponse> ToDetailResponseAsync(
        Achievement achievement)
    {
        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
            achievement.RewardPackageId);

        if (rewardPackage == null)
        {
            throw new NotFoundException("Reward package not found");
        }

        var rewardItems = await GetRewardItemResponsesAsync(
            achievement.RewardPackageId);
        var conditions = (await _achievementConditionRepository.FindAsync(x =>
                x.AchievementId == achievement.AchievementId))
            .ToList();

        return new AdminAchievementDetailResponse
        {
            AchievementId = achievement.AchievementId,
            Title = achievement.Title,
            Description = achievement.Description,
            IconUrl = achievement.IconUrl,
            StatusName = GetStatusName(achievement.IsActive),
            IsActive = achievement.IsActive,
            MetricCode = achievement.MetricCode,
            TargetValue = achievement.TargetValue,
            WalletAmount = rewardPackage.WalletAmount,
            RewardItems = rewardItems,
            AssignmentConditions = conditions
                .Where(x => x.ConditionGroup == AssignmentConditionGroup)
                .Select(ToConditionResponse)
                .ToList()
        };
    }

    private static AdminAchievementListItemResponse ToListItemResponse(
        Achievement achievement,
        IReadOnlyDictionary<Guid, RewardPackage> rewardPackages,
        IReadOnlyCollection<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyCollection<AchievementCondition> conditions)
    {
        rewardPackages.TryGetValue(achievement.RewardPackageId, out var rewardPackage);
        var achievementRewardItems = rewardItems
            .Where(x => x.RewardPackageId == achievement.RewardPackageId)
            .ToList();
        var completionCondition = conditions
            .FirstOrDefault(x =>
                x.AchievementId == achievement.AchievementId
                && x.ConditionGroup == CompletionConditionGroup);

        return new AdminAchievementListItemResponse
        {
            AchievementId = achievement.AchievementId,
            Title = achievement.Title,
            Description = achievement.Description,
            IconUrl = achievement.IconUrl,
            ConditionText = GetConditionText(achievement.MetricCode, achievement.TargetValue),
            RewardText = GetRewardText(
                rewardPackage,
                achievementRewardItems,
                items),
            StatusName = GetStatusName(achievement.IsActive),
            IsActive = achievement.IsActive
        };
    }

    private async Task<List<AdminAchievementRewardItemResponse>>
        GetRewardItemResponsesAsync(Guid rewardPackageId)
    {
        var rewardItems = (await _rewardPackageItemRepository.FindAsync(x =>
                x.RewardPackageId == rewardPackageId))
            .ToList();
        var items = await GetItemsByRewardItemsAsync(rewardItems);

        return rewardItems
            .Select(x =>
            {
                items.TryGetValue(x.ItemId, out var item);

                return new AdminAchievementRewardItemResponse
                {
                    ItemId = x.ItemId,
                    ItemName = item?.ItemName ?? string.Empty,
                    Quantity = x.Quantity
                };
            })
            .ToList();
    }

    private async Task<Dictionary<Guid, Item>> GetItemsByRewardItemsAsync(
        IReadOnlyCollection<RewardPackageItem> rewardItems)
    {
        var itemIds = rewardItems
            .Select(x => x.ItemId)
            .Distinct()
            .ToArray();

        if (itemIds.Length == 0)
        {
            return [];
        }

        return (await _itemRepository.FindAsync(x => itemIds.Contains(x.ItemId)))
            .ToDictionary(x => x.ItemId);
    }

    private async Task AddRewardItemsAsync(
        Guid rewardPackageId,
        IEnumerable<AdminAchievementRewardItemRequest> rewardItems)
    {
        foreach (var rewardItem in MergeRewardItems(rewardItems))
        {
            await _rewardPackageItemRepository.AddAsync(new RewardPackageItem
            {
                RewardPackageId = rewardPackageId,
                ItemId = rewardItem.ItemId,
                Quantity = rewardItem.Quantity
            });
        }
    }

    private async Task ReplaceRewardItemsAsync(
        Guid rewardPackageId,
        IEnumerable<AdminAchievementRewardItemRequest> rewardItems)
    {
        var currentRewardItems = await _rewardPackageItemRepository.FindAsync(x =>
            x.RewardPackageId == rewardPackageId);

        foreach (var rewardItem in currentRewardItems)
        {
            _rewardPackageItemRepository.Delete(rewardItem);
        }

        await AddRewardItemsAsync(rewardPackageId, rewardItems);
    }

    private async Task AddConditionsAsync(
        Guid achievementId,
        IEnumerable<AdminAchievementConditionRequest> assignmentConditions)
    {

        foreach (var condition in assignmentConditions)
        {
            await AddConditionAsync(
                achievementId,
                AssignmentConditionGroup,
                condition);
        }
    }

    private async Task ReplaceConditionsAsync(
        Guid achievementId,
        IEnumerable<AdminAchievementConditionRequest> assignmentConditions)
    {
        var currentConditions = await _achievementConditionRepository.FindAsync(x =>
            x.AchievementId == achievementId);

        foreach (var condition in currentConditions)
        {
            _achievementConditionRepository.Delete(condition);
        }

        await AddConditionsAsync(
            achievementId,
            assignmentConditions);
    }

    private async Task AddConditionAsync(
        Guid achievementId,
        string conditionGroup,
        AdminAchievementConditionRequest condition)
    {
        await _achievementConditionRepository.AddAsync(new AchievementCondition
        {
            AchievementConditionId = Guid.NewGuid(),
            AchievementId = achievementId,
            ConditionGroup = conditionGroup,
            ConditionCode = MissionMetricCodeCatalog.NormalizeOrThrow(
                condition.ConditionCode),
            TargetValue = condition.TargetValue,
            ReferenceAchievementId = condition.ReferenceAchievementId,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task EnsureValidRequestAsync(
        CreateAdminAchievementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BadRequestException("Title is required");
        }

        if (request.WalletAmount < 0)
        {
            throw new BadRequestException(
                "Wallet amount must be greater than or equal to 0");
        }

        if (request.WalletAmount == 0 && request.RewardItems.Count == 0)
        {
            throw new BadRequestException("Achievement reward is required");
        }

        MissionMetricCodeCatalog.NormalizeOrThrow(request.MetricCode);
        if (request.TargetValue <= 0)
        {
            throw new BadRequestException("Target value must be greater than 0");
        }

        EnsureValidRewardItems(request.RewardItems);
        EnsureValidConditions(
            request.AssignmentConditions,
            AssignmentConditionGroup);
        await EnsureRewardItemsExistAsync(request.RewardItems);
        await EnsureReferenceAchievementsExistAsync(request.AssignmentConditions);
    }

    private async Task EnsureValidUpdateRequestAsync(
        UpdateAdminAchievementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BadRequestException("Title is required");
        }

        if (request.WalletAmount < 0)
        {
            throw new BadRequestException(
                "Wallet amount must be greater than or equal to 0");
        }

        if (request.WalletAmount == 0 && request.RewardItems.Count == 0)
        {
            throw new BadRequestException("Achievement reward is required");
        }

        MissionMetricCodeCatalog.NormalizeOrThrow(request.MetricCode);
        if (request.TargetValue <= 0)
        {
            throw new BadRequestException("Target value must be greater than 0");
        }

        EnsureValidRewardItems(request.RewardItems);
        EnsureValidConditions(
            request.AssignmentConditions,
            AssignmentConditionGroup);
        await EnsureRewardItemsExistAsync(request.RewardItems);
        await EnsureReferenceAchievementsExistAsync(request.AssignmentConditions);
    }

    private static void EnsureValidRewardItems(
        IEnumerable<AdminAchievementRewardItemRequest> rewardItems)
    {
        foreach (var rewardItem in rewardItems)
        {
            if (rewardItem.ItemId == Guid.Empty)
            {
                throw new BadRequestException("Reward item id is required");
            }

            if (rewardItem.Quantity <= 0)
            {
                throw new BadRequestException(
                    "Reward item quantity must be greater than 0");
            }
        }
    }

    private static void EnsureValidConditions(
        IEnumerable<AdminAchievementConditionRequest> conditions,
        string conditionGroup)
    {
        foreach (var condition in conditions)
        {
            MissionMetricCodeCatalog.NormalizeOrThrow(condition.ConditionCode);

            if (condition.TargetValue <= 0)
            {
                throw new BadRequestException(
                    $"{conditionGroup} condition target value must be greater than 0");
            }
        }
    }

    private async Task EnsureRewardItemsExistAsync(
        IEnumerable<AdminAchievementRewardItemRequest> rewardItems)
    {
        var itemIds = rewardItems
            .Select(x => x.ItemId)
            .Distinct()
            .ToArray();

        if (itemIds.Length == 0)
        {
            return;
        }

        var existingItemIds = (await _itemRepository.FindAsync(x =>
                itemIds.Contains(x.ItemId) && x.IsActive))
            .Select(x => x.ItemId)
            .ToHashSet();

        if (itemIds.Any(itemId => !existingItemIds.Contains(itemId)))
        {
            throw new BadRequestException("Reward item is invalid");
        }
    }

    private async Task EnsureReferenceAchievementsExistAsync(
        IEnumerable<AdminAchievementConditionRequest> conditions)
    {
        var referenceAchievementIds = conditions
            .Where(x => x.ReferenceAchievementId.HasValue)
            .Select(x => x.ReferenceAchievementId!.Value)
            .Distinct()
            .ToArray();

        if (referenceAchievementIds.Length == 0)
        {
            return;
        }

        var existingAchievementIds = (await _achievementRepository.FindAsync(x =>
                referenceAchievementIds.Contains(x.AchievementId)))
            .Select(x => x.AchievementId)
            .ToHashSet();

        if (referenceAchievementIds.Any(achievementId =>
                !existingAchievementIds.Contains(achievementId)))
        {
            throw new BadRequestException("Reference achievement is invalid");
        }
    }

    private static List<AdminAchievementRewardItemRequest> MergeRewardItems(
        IEnumerable<AdminAchievementRewardItemRequest> rewardItems)
    {
        return rewardItems
            .GroupBy(x => x.ItemId)
            .Select(x => new AdminAchievementRewardItemRequest
            {
                ItemId = x.Key,
                Quantity = x.Sum(item => item.Quantity)
            })
            .ToList();
    }

    private static List<AdminAchievementConditionRequest> NormalizeConditions(
        IEnumerable<AdminAchievementConditionRequest> conditions)
    {
        return conditions
            .Select(x => new AdminAchievementConditionRequest
            {
                ConditionCode = MissionMetricCodeCatalog.NormalizeOrThrow(
                    x.ConditionCode),
                TargetValue = x.TargetValue,
                ReferenceAchievementId = x.ReferenceAchievementId
            })
            .ToList();
    }

    private static AdminAchievementConditionResponse ToConditionResponse(
        AchievementCondition condition)
    {
        return new AdminAchievementConditionResponse
        {
            AchievementConditionId = condition.AchievementConditionId,
            ConditionGroup = condition.ConditionGroup,
            ConditionCode = condition.ConditionCode,
            TargetValue = condition.TargetValue,
            ReferenceAchievementId = condition.ReferenceAchievementId
        };
    }

    private static string GetStatusName(bool isActive)
    {
        return isActive ? "Hoat dong" : "Tam dung";
    }

    private static string GetConditionText(string conditionCode, int targetValue)
    {
        return MissionMetricCodeCatalog.GetTargetText(conditionCode, targetValue);
    }

    private static string GetRewardText(
        RewardPackage? rewardPackage,
        IReadOnlyCollection<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items)
    {
        var parts = new List<string>();

        if (rewardPackage?.WalletAmount > 0)
        {
            parts.Add($"{rewardPackage.WalletAmount:N0} Giot Suong");
        }

        parts.AddRange(rewardItems.Select(x =>
        {
            items.TryGetValue(x.ItemId, out var item);
            var itemName = string.IsNullOrWhiteSpace(item?.ItemName)
                ? x.ItemId.ToString()
                : item.ItemName;

            return $"{itemName} x{x.Quantity:N0}";
        }));

        return string.Join(", ", parts);
    }
}
