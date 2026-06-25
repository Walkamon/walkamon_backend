using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace BLL.Service;

public class AdminChallengeService : IAdminChallengeService
{
    private const string ChallengeMissionTypeCode = "challenge";

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;
    private readonly IGenericRepository<UserMission> _userMissionRepository;

    public AdminChallengeService(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<RewardPackage> rewardPackageRepository,
        IGenericRepository<RewardPackageItem> rewardPackageItemRepository,
        IGenericRepository<Item> itemRepository,
        IGenericRepository<UserMission> userMissionRepository)
    {
        _missionRepository = missionRepository;
        _rewardPackageRepository = rewardPackageRepository;
        _rewardPackageItemRepository = rewardPackageItemRepository;
        _itemRepository = itemRepository;
        _userMissionRepository = userMissionRepository;
    }

    public async Task<AdminChallengeListResponse> GetChallengesAsync(
        string? search,
        string? status)
    {
        var missions = (await _missionRepository.FindAsync(x =>
                x.MissionTypeCode == ChallengeMissionTypeCode))
            .ToList();
        var participantCounts = await GetParticipantCountsAsync();
        var now = DateTime.UtcNow;

        var filtered = missions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            filtered = filtered.Where(x =>
                x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Description)
                    && x.Description.Contains(
                        keyword,
                        StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x =>
                GetStatus(x, now).Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var challenges = filtered
            .OrderByDescending(x => x.StartAt ?? DateTime.MinValue)
            .ThenBy(x => x.Title)
            .Select(x => ToListResponse(x, participantCounts, now))
            .ToList();

        return new AdminChallengeListResponse
        {
            Summary = new AdminChallengeSummaryResponse
            {
                TotalChallenges = missions.Count,
                OngoingChallenges = missions.Count(x => GetStatus(x, now) == "ongoing"),
                TotalParticipants = missions.Sum(x =>
                    participantCounts.TryGetValue(x.MissionId, out var count) ? count : 0)
            },
            Challenges = challenges
        };
    }

    public async Task<AdminChallengeDetailResponse> GetChallengeDetailAsync(
        Guid challengeId)
    {
        var mission = await GetMissionOrThrowAsync(challengeId);
        var participantCounts = await GetParticipantCountsAsync();

        return await ToDetailResponseAsync(
            mission,
            participantCounts.TryGetValue(mission.MissionId, out var count) ? count : 0);
    }

    public async Task<AdminChallengeDetailResponse> CreateChallengeAsync(
        CreateAdminChallengeRequest request)
    {
        var metricCode = MissionMetricCodeCatalog.NormalizeOrThrow(
            request.MetricCode);
        EnsureValidRequest(request);
        await EnsureRewardItemsExistAsync(request.RewardItems);

        var rewardPackage = new RewardPackage
        {
            RewardPackageId = Guid.NewGuid(),
            PackageName = $"challenge-{Guid.NewGuid():N}",
            WalletAmount = request.WalletAmount
        };

        await _rewardPackageRepository.AddAsync(rewardPackage);

        var mission = new Mission
        {
            MissionId = Guid.NewGuid(),
            MissionTypeCode = ChallengeMissionTypeCode,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            MetricCode = metricCode,
            TargetValue = request.TargetValue,
            RewardPackageId = rewardPackage.RewardPackageId,
            IsCancelable = request.IsCancelable,
            IsActive = request.IsActive,
            StartAt = request.StartAt,
            EndAt = request.EndAt
        };

        await _missionRepository.AddAsync(mission);
        await AddRewardItemsAsync(rewardPackage.RewardPackageId, request.RewardItems);
        await _missionRepository.SaveAsync();

        return await ToDetailResponseAsync(mission, 0);
    }

    public async Task<AdminChallengeDetailResponse> UpdateChallengeAsync(
        Guid challengeId,
        UpdateAdminChallengeRequest request)
    {
        var metricCode = MissionMetricCodeCatalog.NormalizeOrThrow(
            request.MetricCode);
        EnsureValidRequest(request);
        await EnsureRewardItemsExistAsync(request.RewardItems);

        var mission = await GetMissionOrThrowAsync(challengeId);
        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
            mission.RewardPackageId);

        if (rewardPackage == null)
        {
            throw new NotFoundException("Reward package not found");
        }

        mission.Title = request.Title.Trim();
        mission.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        mission.MetricCode = metricCode;
        mission.TargetValue = request.TargetValue;
        mission.IsCancelable = request.IsCancelable;
        mission.IsActive = request.IsActive;
        mission.StartAt = request.StartAt;
        mission.EndAt = request.EndAt;

        rewardPackage.WalletAmount = request.WalletAmount;

        _missionRepository.Update(mission);
        _rewardPackageRepository.Update(rewardPackage);
        await ReplaceRewardItemsAsync(mission.RewardPackageId, request.RewardItems);
        await _missionRepository.SaveAsync();

        var participantCounts = await GetParticipantCountsAsync();
        return await ToDetailResponseAsync(
            mission,
            participantCounts.TryGetValue(mission.MissionId, out var count) ? count : 0);
    }

    public async Task UpdateChallengeStatusAsync(
        Guid challengeId,
        UpdateAdminChallengeStatusRequest request)
    {
        var mission = await GetMissionOrThrowAsync(challengeId);

        mission.IsActive = request.IsActive;
        _missionRepository.Update(mission);
        await _missionRepository.SaveAsync();
    }

    private async Task<Mission> GetMissionOrThrowAsync(Guid challengeId)
    {
        var mission = await _missionRepository.GetByIdAsync(challengeId);

        if (mission == null || mission.MissionTypeCode != ChallengeMissionTypeCode)
        {
            throw new NotFoundException("Challenge not found");
        }

        return mission;
    }

    private async Task<Dictionary<Guid, int>> GetParticipantCountsAsync()
    {
        var userMissions = await _userMissionRepository.GetAllAsync();

        return userMissions
            .GroupBy(x => x.MissionId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(userMission => userMission.UserId).Distinct().Count());
    }

    private async Task<AdminChallengeDetailResponse> ToDetailResponseAsync(
        Mission mission,
        int participants)
    {
        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
            mission.RewardPackageId);

        if (rewardPackage == null)
        {
            throw new NotFoundException("Reward package not found");
        }

        var rewardItems = await GetRewardItemResponsesAsync(
            mission.RewardPackageId);
        var now = DateTime.UtcNow;

        return new AdminChallengeDetailResponse
        {
            ChallengeId = mission.MissionId,
            Title = mission.Title,
            Description = mission.Description,
            MetricCode = mission.MetricCode,
            TargetValue = mission.TargetValue,
            StartAt = mission.StartAt,
            EndAt = mission.EndAt,
            Participants = participants,
            Status = GetStatus(mission, now),
            IsActive = mission.IsActive,
            IsCancelable = mission.IsCancelable,
            WalletAmount = rewardPackage.WalletAmount,
            RewardItems = rewardItems
        };
    }

    private static AdminChallengeListItemResponse ToListResponse(
        Mission mission,
        IReadOnlyDictionary<Guid, int> participantCounts,
        DateTime now)
    {
        return new AdminChallengeListItemResponse
        {
            ChallengeId = mission.MissionId,
            Title = mission.Title,
            Description = mission.Description,
            TargetText = MissionMetricCodeCatalog.GetTargetText(
                mission.MetricCode,
                mission.TargetValue),
            TimeText = GetTimeText(mission.StartAt, mission.EndAt),
            Participants = participantCounts.TryGetValue(
                mission.MissionId,
                out var count)
                    ? count
                    : 0,
            StatusName = GetStatusName(GetStatus(mission, now)),
            IsActive = mission.IsActive
        };
    }

    private async Task<List<AdminChallengeRewardItemResponse>>
        GetRewardItemResponsesAsync(Guid rewardPackageId)
    {
        var rewardItems = (await _rewardPackageItemRepository.FindAsync(x =>
                x.RewardPackageId == rewardPackageId))
            .ToList();

        if (rewardItems.Count == 0)
        {
            return [];
        }

        var itemIds = rewardItems.Select(x => x.ItemId).ToHashSet();
        var items = (await _itemRepository.FindAsync(x => itemIds.Contains(x.ItemId)))
            .ToDictionary(x => x.ItemId);

        return rewardItems
            .Select(x =>
            {
                items.TryGetValue(x.ItemId, out var item);

                return new AdminChallengeRewardItemResponse
                {
                    ItemId = x.ItemId,
                    ItemName = item?.ItemName ?? string.Empty,
                    Quantity = x.Quantity
                };
            })
            .ToList();
    }

    private async Task AddRewardItemsAsync(
        Guid rewardPackageId,
        IEnumerable<AdminChallengeRewardItemRequest> rewardItems)
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
        IEnumerable<AdminChallengeRewardItemRequest> rewardItems)
    {
        var currentRewardItems = await _rewardPackageItemRepository.FindAsync(x =>
            x.RewardPackageId == rewardPackageId);

        foreach (var rewardItem in currentRewardItems)
        {
            _rewardPackageItemRepository.Delete(rewardItem);
        }

        await AddRewardItemsAsync(rewardPackageId, rewardItems);
    }

    private async Task EnsureRewardItemsExistAsync(
        IEnumerable<AdminChallengeRewardItemRequest> rewardItems)
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

    private static List<AdminChallengeRewardItemRequest> MergeRewardItems(
        IEnumerable<AdminChallengeRewardItemRequest> rewardItems)
    {
        return rewardItems
            .GroupBy(x => x.ItemId)
            .Select(x => new AdminChallengeRewardItemRequest
            {
                ItemId = x.Key,
                Quantity = x.Sum(item => item.Quantity)
            })
            .ToList();
    }

    private static void EnsureValidRequest(CreateAdminChallengeRequest request)
    {
        if (request.TargetValue <= 0)
        {
            throw new BadRequestException("Target value must be greater than 0");
        }

        if (request.StartAt.HasValue
            && request.EndAt.HasValue
            && request.StartAt > request.EndAt)
        {
            throw new BadRequestException(
                "Start date must be before or equal to end date");
        }

        if (request.WalletAmount < 0)
        {
            throw new BadRequestException(
                "Wallet amount must be greater than or equal to 0");
        }

        if (request.WalletAmount == 0 && request.RewardItems.Count == 0)
        {
            throw new BadRequestException("Challenge reward is required");
        }

        if (request.RewardItems.Any(x => x.Quantity <= 0))
        {
            throw new BadRequestException(
                "Reward item quantity must be greater than 0");
        }
    }

    private static string GetStatus(Mission mission, DateTime now)
    {
        if (!mission.IsActive)
        {
            return "inactive";
        }

        if (mission.StartAt.HasValue && mission.StartAt.Value > now)
        {
            return "upcoming";
        }

        if (mission.EndAt.HasValue && mission.EndAt.Value < now)
        {
            return "ended";
        }

        return "ongoing";
    }

    private static string GetStatusName(string status)
    {
        return status switch
        {
            "inactive" => "Đã kết thúc",
            "upcoming" => "Sắp diễn ra",
            "ongoing" => "Đang diễn ra",
            "ended" => "Đã kết thúc",
            _ => status
        };
    }

    private static string GetTargetText(Mission mission)
    {
        var value = mission.TargetValue.ToString("N0");

        return mission.MetricCode switch
        {
            "steps" => $"{value} bước",
            "mission" or "missions" => $"Hoàn thành {value} nhiệm vụ",
            _ => $"{value} {mission.MetricCode}"
        };
    }

    private static string GetTimeText(DateTime? startAt, DateTime? endAt)
    {
        var startText = startAt?.ToString("yyyy-MM-dd") ?? string.Empty;
        var endText = endAt?.ToString("yyyy-MM-dd") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(startText))
        {
            return endText;
        }

        if (string.IsNullOrWhiteSpace(endText) || startText == endText)
        {
            return startText;
        }

        return $"{startText} đến {endText}";
    }
}
