using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace BLL.Service;

public class AdminMissionService : IAdminMissionService
{
    private const string DailyMissionTypeCode = "daily";
    private const string WeeklyMissionTypeCode = "weekly";
    private const string MonthlyMissionTypeCode = "monthly";
    private const string ChallengeMissionTypeCode = "challenge";
    private const string CompletionConditionGroup = "completion";
    private const string AssignmentConditionGroup = "assignment";

    private static readonly string[] OverallMissionTypeCodes =
    [
        WeeklyMissionTypeCode,
        MonthlyMissionTypeCode
    ];

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<MissionCondition> _missionConditionRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;

    public AdminMissionService(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<MissionCondition> missionConditionRepository,
        IGenericRepository<RewardPackage> rewardPackageRepository,
        IGenericRepository<RewardPackageItem> rewardPackageItemRepository,
        IGenericRepository<Item> itemRepository)
    {
        _missionRepository = missionRepository;
        _missionConditionRepository = missionConditionRepository;
        _rewardPackageRepository = rewardPackageRepository;
        _rewardPackageItemRepository = rewardPackageItemRepository;
        _itemRepository = itemRepository;
    }

    public async Task<AdminMissionListResponse> GetDailyMissionsAsync()
    {
        return await GetMissionListAsync([DailyMissionTypeCode]);
    }

    public async Task<AdminMissionDetailResponse> GetDailyMissionDetailAsync(
        Guid missionId)
    {
        var mission = await GetMissionOrThrowAsync(
            missionId,
            [DailyMissionTypeCode]);

        return await ToDetailResponseAsync(mission);
    }

    public async Task<AdminMissionDetailResponse> CreateDailyMissionAsync(
        CreateAdminMissionRequest request)
    {
        return await CreateMissionAsync(DailyMissionTypeCode, request);
    }

    public async Task<AdminMissionDetailResponse> UpdateDailyMissionAsync(
        Guid missionId,
        UpdateAdminMissionRequest request)
    {
        return await UpdateMissionAsync(
            missionId,
            DailyMissionTypeCode,
            request,
            [DailyMissionTypeCode]);
    }

    public async Task UpdateDailyMissionStatusAsync(
        Guid missionId,
        UpdateAdminMissionStatusRequest request)
    {
        await UpdateMissionStatusAsync(missionId, request, [DailyMissionTypeCode]);
    }

    public async Task<AdminMissionListResponse> GetOverallMissionsAsync()
    {
        return await GetMissionListAsync(OverallMissionTypeCodes);
    }

    public async Task<AdminMissionDetailResponse> GetOverallMissionDetailAsync(
        Guid missionId)
    {
        var mission = await GetMissionOrThrowAsync(
            missionId,
            OverallMissionTypeCodes);

        return await ToDetailResponseAsync(mission);
    }

    public async Task<AdminMissionDetailResponse> CreateOverallMissionAsync(
        CreateAdminMissionRequest request)
    {
        var missionTypeCode = NormalizeMissionTypeCode(request.MissionTypeCode);

        if (!OverallMissionTypeCodes.Contains(missionTypeCode))
        {
            throw new BadRequestException(
                "Overall mission type must be weekly or monthly");
        }

        return await CreateMissionAsync(missionTypeCode, request);
    }

    public async Task<AdminMissionDetailResponse> UpdateOverallMissionAsync(
        Guid missionId,
        UpdateAdminMissionRequest request)
    {
        var missionTypeCode = NormalizeMissionTypeCode(request.MissionTypeCode);

        if (!OverallMissionTypeCodes.Contains(missionTypeCode))
        {
            throw new BadRequestException(
                "Overall mission type must be weekly or monthly");
        }

        return await UpdateMissionAsync(
            missionId,
            missionTypeCode,
            request,
            OverallMissionTypeCodes);
    }

    public async Task UpdateOverallMissionStatusAsync(
        Guid missionId,
        UpdateAdminMissionStatusRequest request)
    {
        await UpdateMissionStatusAsync(missionId, request, OverallMissionTypeCodes);
    }

    private async Task<AdminMissionListResponse> GetMissionListAsync(
        IReadOnlyCollection<string> missionTypeCodes)
    {
        var missions = (await _missionRepository.FindAsync(x =>
                missionTypeCodes.Contains(x.MissionTypeCode)))
            .OrderBy(x => x.MissionTypeCode)
            .ThenBy(x => x.Title)
            .ToList();
        var missionIds = missions
            .Select(x => x.MissionId)
            .ToArray();

        var rewardPackageIds = missions
            .Select(x => x.RewardPackageId)
            .Distinct()
            .ToArray();
        var rewardPackages = (await _rewardPackageRepository.FindAsync(x =>
                rewardPackageIds.Contains(x.RewardPackageId)))
            .ToDictionary(x => x.RewardPackageId);
        var rewardItems = (await _rewardPackageItemRepository.FindAsync(x =>
                rewardPackageIds.Contains(x.RewardPackageId)))
            .ToList();
        var conditions = (await _missionConditionRepository.FindAsync(x =>
                missionIds.Contains(x.MissionId)))
            .ToList();
        var items = await GetItemsByRewardItemsAsync(rewardItems);

        return new AdminMissionListResponse
        {
            Summary = new AdminMissionSummaryResponse
            {
                TotalMissions = missions.Count,
                ActiveMissions = missions.Count(x => x.IsActive),
                WeeklyMissions = missions.Count(x =>
                    x.MissionTypeCode == WeeklyMissionTypeCode),
                MonthlyMissions = missions.Count(x =>
                    x.MissionTypeCode == MonthlyMissionTypeCode),
                TotalWalletAmount = missions.Sum(x =>
                    rewardPackages.TryGetValue(x.RewardPackageId, out var rewardPackage)
                        ? rewardPackage.WalletAmount
                        : 0)
            },
            Missions = missions
                .Select(x => ToListItemResponse(
                    x,
                    rewardPackages,
                    rewardItems,
                    items,
                    conditions))
                .ToList()
        };
    }

    private async Task<AdminMissionDetailResponse> CreateMissionAsync(
        string missionTypeCode,
        CreateAdminMissionRequest request)
    {
        await EnsureValidRequestAsync(request, missionTypeCode);

        var completionConditions = NormalizeConditions(
            request.CompletionConditions);
        var assignmentConditions = NormalizeConditions(
            request.AssignmentConditions);
        var firstCompletionCondition = completionConditions[0];
        var rewardPackage = new RewardPackage
        {
            RewardPackageId = Guid.NewGuid(),
            PackageName = $"mission-{Guid.NewGuid():N}",
            WalletAmount = request.WalletAmount
        };
        var mission = new Mission
        {
            MissionId = Guid.NewGuid(),
            MissionTypeCode = missionTypeCode,
            Title = request.Title.Trim(),
            Description = NormalizeOptionalText(request.Description),
            MetricCode = firstCompletionCondition.ConditionCode,
            TargetValue = firstCompletionCondition.TargetValue,
            RewardPackageId = rewardPackage.RewardPackageId,
            IsCancelable = false,
            IsActive = request.IsActive
        };

        await _rewardPackageRepository.AddAsync(rewardPackage);
        await _missionRepository.AddAsync(mission);
        await AddRewardItemsAsync(
            rewardPackage.RewardPackageId,
            request.RewardItems);
        await AddConditionsAsync(
            mission.MissionId,
            completionConditions,
            assignmentConditions);
        await _missionRepository.SaveAsync();

        return await ToDetailResponseAsync(mission);
    }

    private async Task<AdminMissionDetailResponse> UpdateMissionAsync(
        Guid missionId,
        string missionTypeCode,
        UpdateAdminMissionRequest request,
        IReadOnlyCollection<string> allowedMissionTypeCodes)
    {
        await EnsureValidRequestAsync(request, missionTypeCode);

        var mission = await GetMissionOrThrowAsync(
            missionId,
            allowedMissionTypeCodes);
        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
            mission.RewardPackageId);

        if (rewardPackage == null)
        {
            throw new NotFoundException("Reward package not found");
        }

        var completionConditions = NormalizeConditions(
            request.CompletionConditions);
        var assignmentConditions = NormalizeConditions(
            request.AssignmentConditions);
        var firstCompletionCondition = completionConditions[0];

        mission.MissionTypeCode = missionTypeCode;
        mission.Title = request.Title.Trim();
        mission.Description = NormalizeOptionalText(request.Description);
        mission.MetricCode = firstCompletionCondition.ConditionCode;
        mission.TargetValue = firstCompletionCondition.TargetValue;
        mission.IsActive = request.IsActive;
        rewardPackage.WalletAmount = request.WalletAmount;

        _missionRepository.Update(mission);
        _rewardPackageRepository.Update(rewardPackage);
        await ReplaceRewardItemsAsync(
            mission.RewardPackageId,
            request.RewardItems);
        await ReplaceConditionsAsync(
            mission.MissionId,
            completionConditions,
            assignmentConditions);
        await _missionRepository.SaveAsync();

        return await ToDetailResponseAsync(mission);
    }

    private async Task UpdateMissionStatusAsync(
        Guid missionId,
        UpdateAdminMissionStatusRequest request,
        IReadOnlyCollection<string> allowedMissionTypeCodes)
    {
        var mission = await GetMissionOrThrowAsync(
            missionId,
            allowedMissionTypeCodes);

        mission.IsActive = request.IsActive;
        _missionRepository.Update(mission);
        await _missionRepository.SaveAsync();
    }

    private async Task<Mission> GetMissionOrThrowAsync(
        Guid missionId,
        IReadOnlyCollection<string> allowedMissionTypeCodes)
    {
        var mission = await _missionRepository.GetByIdAsync(missionId);

        if (mission == null
            || !allowedMissionTypeCodes.Contains(mission.MissionTypeCode)
            || mission.MissionTypeCode == ChallengeMissionTypeCode)
        {
            throw new NotFoundException("Mission not found");
        }

        return mission;
    }

    private async Task<AdminMissionDetailResponse> ToDetailResponseAsync(
        Mission mission)
    {
        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
            mission.RewardPackageId);

        if (rewardPackage == null)
        {
            throw new NotFoundException("Reward package not found");
        }

        var rewardItems = await GetRewardItemResponsesAsync(
            mission.RewardPackageId);
        var conditions = (await _missionConditionRepository.FindAsync(x =>
                x.MissionId == mission.MissionId))
            .ToList();

        return new AdminMissionDetailResponse
        {
            MissionId = mission.MissionId,
            Title = mission.Title,
            Description = mission.Description,
            MissionTypeCode = mission.MissionTypeCode,
            StatusName = GetStatusName(mission.IsActive),
            IsActive = mission.IsActive,
            WalletAmount = rewardPackage.WalletAmount,
            RewardItems = rewardItems,
            CompletionConditions = conditions
                .Where(x => x.ConditionGroup == CompletionConditionGroup)
                .Select(ToConditionResponse)
                .ToList(),
            AssignmentConditions = conditions
                .Where(x => x.ConditionGroup == AssignmentConditionGroup)
                .Select(ToConditionResponse)
                .ToList()
        };
    }

    private static AdminMissionListItemResponse ToListItemResponse(
        Mission mission,
        IReadOnlyDictionary<Guid, RewardPackage> rewardPackages,
        IReadOnlyCollection<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyCollection<MissionCondition> conditions)
    {
        rewardPackages.TryGetValue(mission.RewardPackageId, out var rewardPackage);
        var missionRewardItems = rewardItems
            .Where(x => x.RewardPackageId == mission.RewardPackageId)
            .ToList();
        var completionCondition = conditions
            .FirstOrDefault(x =>
                x.MissionId == mission.MissionId
                && x.ConditionGroup == CompletionConditionGroup);

        return new AdminMissionListItemResponse
        {
            MissionId = mission.MissionId,
            Title = mission.Title,
            ConditionText = completionCondition == null
                ? GetConditionText(mission.MetricCode, mission.TargetValue)
                : GetConditionText(
                    completionCondition.ConditionCode,
                    completionCondition.TargetValue),
            RewardText = GetRewardText(
                rewardPackage,
                missionRewardItems,
                items),
            StatusName = GetStatusName(mission.IsActive),
            IsActive = mission.IsActive
        };
    }

    private async Task<List<AdminMissionRewardItemResponse>>
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

                return new AdminMissionRewardItemResponse
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
        IEnumerable<AdminMissionRewardItemRequest> rewardItems)
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
        IEnumerable<AdminMissionRewardItemRequest> rewardItems)
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
        Guid missionId,
        IEnumerable<AdminMissionConditionRequest> completionConditions,
        IEnumerable<AdminMissionConditionRequest> assignmentConditions)
    {
        foreach (var condition in completionConditions)
        {
            await AddConditionAsync(
                missionId,
                CompletionConditionGroup,
                condition);
        }

        foreach (var condition in assignmentConditions)
        {
            await AddConditionAsync(
                missionId,
                AssignmentConditionGroup,
                condition);
        }
    }

    private async Task ReplaceConditionsAsync(
        Guid missionId,
        IEnumerable<AdminMissionConditionRequest> completionConditions,
        IEnumerable<AdminMissionConditionRequest> assignmentConditions)
    {
        var currentConditions = await _missionConditionRepository.FindAsync(x =>
            x.MissionId == missionId);

        foreach (var condition in currentConditions)
        {
            _missionConditionRepository.Delete(condition);
        }

        await AddConditionsAsync(
            missionId,
            completionConditions,
            assignmentConditions);
    }

    private async Task AddConditionAsync(
        Guid missionId,
        string conditionGroup,
        AdminMissionConditionRequest condition)
    {
        await _missionConditionRepository.AddAsync(new MissionCondition
        {
            MissionConditionId = Guid.NewGuid(),
            MissionId = missionId,
            ConditionGroup = conditionGroup,
            ConditionCode = MissionMetricCodeCatalog.NormalizeOrThrow(
                condition.ConditionCode),
            TargetValue = condition.TargetValue,
            ReferenceMissionId = condition.ReferenceMissionId,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task EnsureValidRequestAsync(
        CreateAdminMissionRequest request,
        string missionTypeCode)
    {
        if (missionTypeCode == ChallengeMissionTypeCode)
        {
            throw new BadRequestException("Challenge is managed by challenge API");
        }

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
            throw new BadRequestException("Mission reward is required");
        }

        if (request.CompletionConditions.Count == 0)
        {
            throw new BadRequestException("Completion condition is required");
        }

        EnsureValidRewardItems(request.RewardItems);
        EnsureValidConditions(
            request.CompletionConditions,
            CompletionConditionGroup);
        EnsureValidConditions(
            request.AssignmentConditions,
            AssignmentConditionGroup);
        await EnsureRewardItemsExistAsync(request.RewardItems);
        await EnsureReferenceMissionsExistAsync(request.AssignmentConditions);
    }

    private static void EnsureValidRewardItems(
        IEnumerable<AdminMissionRewardItemRequest> rewardItems)
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
        IEnumerable<AdminMissionConditionRequest> conditions,
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
        IEnumerable<AdminMissionRewardItemRequest> rewardItems)
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

    private async Task EnsureReferenceMissionsExistAsync(
        IEnumerable<AdminMissionConditionRequest> conditions)
    {
        var referenceMissionIds = conditions
            .Where(x => x.ReferenceMissionId.HasValue)
            .Select(x => x.ReferenceMissionId!.Value)
            .Distinct()
            .ToArray();

        if (referenceMissionIds.Length == 0)
        {
            return;
        }

        var existingMissionIds = (await _missionRepository.FindAsync(x =>
                referenceMissionIds.Contains(x.MissionId)
                && x.MissionTypeCode != ChallengeMissionTypeCode))
            .Select(x => x.MissionId)
            .ToHashSet();

        if (referenceMissionIds.Any(missionId =>
                !existingMissionIds.Contains(missionId)))
        {
            throw new BadRequestException("Reference mission is invalid");
        }
    }

    private static List<AdminMissionRewardItemRequest> MergeRewardItems(
        IEnumerable<AdminMissionRewardItemRequest> rewardItems)
    {
        return rewardItems
            .GroupBy(x => x.ItemId)
            .Select(x => new AdminMissionRewardItemRequest
            {
                ItemId = x.Key,
                Quantity = x.Sum(item => item.Quantity)
            })
            .ToList();
    }

    private static List<AdminMissionConditionRequest> NormalizeConditions(
        IEnumerable<AdminMissionConditionRequest> conditions)
    {
        return conditions
            .Select(x => new AdminMissionConditionRequest
            {
                ConditionCode = MissionMetricCodeCatalog.NormalizeOrThrow(
                    x.ConditionCode),
                TargetValue = x.TargetValue,
                ReferenceMissionId = x.ReferenceMissionId
            })
            .ToList();
    }

    private static AdminMissionConditionResponse ToConditionResponse(
        MissionCondition condition)
    {
        return new AdminMissionConditionResponse
        {
            MissionConditionId = condition.MissionConditionId,
            ConditionGroup = condition.ConditionGroup,
            ConditionCode = condition.ConditionCode,
            TargetValue = condition.TargetValue,
            ReferenceMissionId = condition.ReferenceMissionId
        };
    }

    private static string NormalizeMissionTypeCode(string? missionTypeCode)
    {
        return string.IsNullOrWhiteSpace(missionTypeCode)
            ? string.Empty
            : missionTypeCode.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
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
