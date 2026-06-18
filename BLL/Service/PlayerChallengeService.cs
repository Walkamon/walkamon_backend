using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace BLL.Service;

public class PlayerChallengeService : IPlayerChallengeService
{
    private const string ChallengeMissionTypeCode = "challenge";
    private const string ActiveStatusCode = "active";
    private const string CancelledStatusCode = "cancelled";
    private const int DailyCancelLimit = 3;

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<UserMission> _userMissionRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;

    public PlayerChallengeService(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<UserMission> userMissionRepository,
        IGenericRepository<RewardPackage> rewardPackageRepository,
        IGenericRepository<RewardPackageItem> rewardPackageItemRepository,
        IGenericRepository<Item> itemRepository)
    {
        _missionRepository = missionRepository;
        _userMissionRepository = userMissionRepository;
        _rewardPackageRepository = rewardPackageRepository;
        _rewardPackageItemRepository = rewardPackageItemRepository;
        _itemRepository = itemRepository;
    }

    public async Task<PlayerChallengeStateResponse> GetRandomChallengeStateAsync(
        Guid userId)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentChallenge = await GetCurrentActiveChallengeAsync(userId, now);
        var cancelUsed = await GetCancelUsedAsync(userId, today);

        return await ToStateResponseAsync(
            currentChallenge,
            cancelUsed);
    }

    public async Task<PlayerChallengeStateResponse> CreateRandomChallengeAsync(
        Guid userId)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentChallenge = await GetCurrentActiveChallengeAsync(userId, now);

        if (currentChallenge != null)
        {
            throw new BadRequestException("Challenge already active");
        }

        var receivedToday = (await _userMissionRepository.FindAsync(x =>
                x.UserId == userId
                && x.CycleDate == today))
            .Select(x => x.MissionId)
            .ToHashSet();

        var availableChallenges = (await _missionRepository.FindAsync(x =>
                x.MissionTypeCode == ChallengeMissionTypeCode
                && x.IsActive
                && (!x.StartAt.HasValue || x.StartAt.Value <= now)
                && (!x.EndAt.HasValue || x.EndAt.Value >= now)))
            .Where(x => !receivedToday.Contains(x.MissionId))
            .ToList();

        if (availableChallenges.Count == 0)
        {
            throw new NotFoundException("No challenge available");
        }

        var challenge = availableChallenges[Random.Shared.Next(availableChallenges.Count)];
        var userMission = new UserMission
        {
            UserMissionId = Guid.NewGuid(),
            UserId = userId,
            MissionId = challenge.MissionId,
            CycleDate = today,
            AssignedAt = now,
            ProgressValue = 0,
            StatusCode = ActiveStatusCode
        };

        await _userMissionRepository.AddAsync(userMission);
        await _userMissionRepository.SaveAsync();

        var cancelUsed = await GetCancelUsedAsync(userId, today);

        return await ToStateResponseAsync(
            (userMission, challenge),
            cancelUsed);
    }

    public async Task<CancelPlayerChallengeResponse> CancelChallengeAsync(
        Guid userId,
        Guid userMissionId)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var userMission = await _userMissionRepository.GetByIdAsync(userMissionId);

        if (userMission == null
            || userMission.UserId != userId)
        {
            throw new NotFoundException("Challenge not found");
        }

        var mission = await _missionRepository.GetByIdAsync(userMission.MissionId);
        if (mission == null
            || mission.MissionTypeCode != ChallengeMissionTypeCode)
        {
            throw new NotFoundException("Challenge not found");
        }

        if (userMission.StatusCode != ActiveStatusCode
            || !IsCurrentChallenge(mission, now))
        {
            throw new BadRequestException("Challenge is not active");
        }

        if (!mission.IsCancelable)
        {
            throw new BadRequestException("Challenge cannot be cancelled");
        }

        var cancelUsed = await GetCancelUsedAsync(userId, today);
        if (cancelUsed >= DailyCancelLimit)
        {
            throw new BadRequestException("Cancel limit reached");
        }

        userMission.StatusCode = CancelledStatusCode;
        _userMissionRepository.Update(userMission);
        await _userMissionRepository.SaveAsync();

        cancelUsed++;

        return new CancelPlayerChallengeResponse
        {
            UserMissionId = userMission.UserMissionId,
            StatusCode = userMission.StatusCode,
            CancelLimit = DailyCancelLimit,
            CancelUsed = cancelUsed,
            CancelRemaining = GetCancelRemaining(cancelUsed)
        };
    }

    private async Task<(UserMission UserMission, Mission Mission)?>
        GetCurrentActiveChallengeAsync(Guid userId, DateTime now)
    {
        var activeUserMissions = (await _userMissionRepository.FindAsync(x =>
                x.UserId == userId
                && x.StatusCode == ActiveStatusCode))
            .ToList();

        if (activeUserMissions.Count == 0)
        {
            return null;
        }

        var missionIds = activeUserMissions
            .Select(x => x.MissionId)
            .ToHashSet();

        var challenges = (await _missionRepository.FindAsync(x =>
                missionIds.Contains(x.MissionId)
                && x.MissionTypeCode == ChallengeMissionTypeCode
                && x.IsActive
                && (!x.StartAt.HasValue || x.StartAt.Value <= now)
                && (!x.EndAt.HasValue || x.EndAt.Value >= now)))
            .ToDictionary(x => x.MissionId);

        foreach (var userMission in activeUserMissions
                     .OrderByDescending(x => x.AssignedAt))
        {
            if (challenges.TryGetValue(userMission.MissionId, out var challenge))
            {
                return (userMission, challenge);
            }
        }

        return null;
    }

    private async Task<int> GetCancelUsedAsync(Guid userId, DateOnly cycleDate)
    {
        var cancelledChallenges = await _userMissionRepository.FindAsync(x =>
            x.UserId == userId
            && x.CycleDate == cycleDate
            && x.StatusCode == CancelledStatusCode);

        var missionIds = cancelledChallenges
            .Select(x => x.MissionId)
            .ToHashSet();

        if (missionIds.Count == 0)
        {
            return 0;
        }

        var challengeIds = (await _missionRepository.FindAsync(x =>
                missionIds.Contains(x.MissionId)
                && x.MissionTypeCode == ChallengeMissionTypeCode))
            .Select(x => x.MissionId)
            .ToHashSet();

        return missionIds.Count(x => challengeIds.Contains(x));
    }

    private async Task<PlayerChallengeStateResponse> ToStateResponseAsync(
        (UserMission UserMission, Mission Mission)? currentChallenge,
        int cancelUsed)
    {
        return new PlayerChallengeStateResponse
        {
            CancelLimit = DailyCancelLimit,
            CancelUsed = cancelUsed,
            CancelRemaining = GetCancelRemaining(cancelUsed),
            CurrentChallenge = currentChallenge.HasValue
                ? await ToChallengeResponseAsync(
                    currentChallenge.Value.UserMission,
                    currentChallenge.Value.Mission)
                : null
        };
    }

    private async Task<PlayerChallengeResponse> ToChallengeResponseAsync(
        UserMission userMission,
        Mission mission)
    {
        var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
            mission.RewardPackageId);

        if (rewardPackage == null)
        {
            throw new NotFoundException("Reward package not found");
        }

        return new PlayerChallengeResponse
        {
            UserMissionId = userMission.UserMissionId,
            ChallengeId = mission.MissionId,
            Title = mission.Title,
            Description = mission.Description,
            MetricCode = mission.MetricCode,
            ProgressValue = userMission.ProgressValue,
            TargetValue = mission.TargetValue,
            WalletAmount = rewardPackage.WalletAmount,
            RewardItems = await GetRewardItemsAsync(rewardPackage.RewardPackageId),
            IsCancelable = mission.IsCancelable,
            StatusCode = userMission.StatusCode,
            AssignedAt = userMission.AssignedAt
        };
    }

    private async Task<List<PlayerChallengeRewardItemResponse>> GetRewardItemsAsync(
        Guid rewardPackageId)
    {
        var rewardItems = (await _rewardPackageItemRepository.FindAsync(x =>
                x.RewardPackageId == rewardPackageId))
            .ToList();

        if (rewardItems.Count == 0)
        {
            return [];
        }

        var itemIds = rewardItems.Select(x => x.ItemId).ToHashSet();
        var items = (await _itemRepository.FindAsync(x =>
                itemIds.Contains(x.ItemId)))
            .ToDictionary(x => x.ItemId);

        return rewardItems
            .Where(x => items.ContainsKey(x.ItemId))
            .Select(x => new PlayerChallengeRewardItemResponse
            {
                ItemId = x.ItemId,
                ItemName = items[x.ItemId].ItemName,
                Quantity = x.Quantity
            })
            .ToList();
    }

    private static bool IsCurrentChallenge(Mission mission, DateTime now)
    {
        return mission.IsActive
            && (!mission.StartAt.HasValue || mission.StartAt.Value <= now)
            && (!mission.EndAt.HasValue || mission.EndAt.Value >= now);
    }

    private static int GetCancelRemaining(int cancelUsed)
    {
        return Math.Max(0, DailyCancelLimit - cancelUsed);
    }
}
