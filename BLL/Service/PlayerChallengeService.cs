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

public class PlayerChallengeService : IPlayerChallengeService
{
    private const string ChallengeMissionTypeCode = "challenge";
    private const string ActiveStatusCode = "active";
    private const string CompletedStatusCode = "completed";
    private const string ClaimedStatusCode = "claimed";
    private const string CancelledStatusCode = "cancelled";
    private const int DailyCancelLimit = 3;

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<UserMission> _userMissionRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;
    private readonly WalkamonContext _context;
    private readonly IAchievementProgressService _achievementProgressService;
    private readonly IMissionProgressService _missionProgressService;

    public PlayerChallengeService(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<UserMission> userMissionRepository,
        IGenericRepository<RewardPackage> rewardPackageRepository,
        IGenericRepository<RewardPackageItem> rewardPackageItemRepository,
        IGenericRepository<Item> itemRepository,
        WalkamonContext context,
        IAchievementProgressService achievementProgressService,
        IMissionProgressService missionProgressService)
    {
        _missionRepository = missionRepository;
        _userMissionRepository = userMissionRepository;
        _rewardPackageRepository = rewardPackageRepository;
        _rewardPackageItemRepository = rewardPackageItemRepository;
        _itemRepository = itemRepository;
        _context = context;
        _achievementProgressService = achievementProgressService;
        _missionProgressService = missionProgressService;
    }

    public async Task<PlayerChallengeStateResponse> GetRandomChallengeStateAsync(
        Guid userId)
    {
        var now = DateTime.UtcNow;
        var today = ChallengeCycleDate.FromUtc(now);
        var currentChallenge = await GetCurrentOpenChallengeAsync(userId, now);
        var cancelUsed = await GetCancelUsedAsync(userId, today);

        return await ToStateResponseAsync(
            currentChallenge,
            cancelUsed,
            now);
    }

    public async Task<PlayerChallengeStateResponse> CreateRandomChallengeAsync(
        Guid userId)
    {
        return await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
        {
            var now = DateTime.UtcNow;
            var today = ChallengeCycleDate.FromUtc(now);
            await LockUserAsync(userId);
            var currentChallenge = await GetCurrentOpenChallengeAsync(userId, now);

            if (currentChallenge != null)
            {
                throw new ConflictException("Challenge already active");
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
                cancelUsed,
                now);
        });
    }

    public async Task<CancelPlayerChallengeResponse> CancelChallengeAsync(
        Guid userId,
        Guid userMissionId)
    {
        return await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
        {
            var now = DateTime.UtcNow;
            var today = ChallengeCycleDate.FromUtc(now);
            await LockUserAsync(userId);
            var userMission = await GetLockedUserMissionAsync(userId, userMissionId);

            if (userMission == null)
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
                || userMission.CycleDate != today
                || !IsCurrentChallenge(mission, now))
            {
                throw new ConflictException("Challenge is not active");
            }

            if (!mission.IsCancelable)
            {
                throw new ConflictException("Challenge cannot be cancelled");
            }

            var cancelUsed = await GetCancelUsedAsync(userId, today);
            if (cancelUsed >= DailyCancelLimit)
            {
                throw new ConflictException("Cancel limit reached");
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
        });
    }

    public async Task<ClaimPlayerChallengeRewardResponse> ClaimChallengeRewardAsync(
        Guid userId,
        Guid userMissionId)
    {
        return await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
        {
            var now = DateTime.UtcNow;
            await LockUserAsync(userId);
            var userMission = await GetLockedUserMissionAsync(userId, userMissionId);

            if (userMission == null)
            {
                throw new NotFoundException("Challenge not found");
            }

            var challenge = await _context.Missions
                .SingleOrDefaultAsync(x =>
                    x.MissionId == userMission.MissionId
                    && x.MissionTypeCode == ChallengeMissionTypeCode);

            if (challenge == null)
            {
                throw new NotFoundException("Challenge not found");
            }

            if (!challenge.IsActive
                || userMission.CycleDate != ChallengeCycleDate.FromUtc(now)
                || (challenge.StartAt.HasValue && challenge.StartAt.Value > now)
                || (challenge.EndAt.HasValue && challenge.EndAt.Value < now))
            {
                throw new ConflictException("Challenge is inactive or expired");
            }

            if (userMission.ClaimedAt.HasValue
                || string.Equals(
                    userMission.StatusCode,
                    ClaimedStatusCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("Challenge reward already claimed");
            }

            if (string.Equals(
                userMission.StatusCode,
                CancelledStatusCode,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("Challenge is cancelled");
            }

            if (userMission.ProgressValue < challenge.TargetValue)
            {
                throw new ConflictException("Challenge is not completed");
            }

            var rewardPackage = await _context.RewardPackages
                .SingleOrDefaultAsync(x =>
                    x.RewardPackageId == challenge.RewardPackageId);

            if (rewardPackage == null)
            {
                throw new ConflictException("Challenge reward is not configured");
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
                throw new ConflictException("Wallet is not available");
            }

            if ((long)wallet.Balance + rewardPackage.WalletAmount > int.MaxValue)
            {
                throw new ConflictException("Wallet balance is too large");
            }

            var rewardItems = await _context.RewardPackageItems
                .Where(x => x.RewardPackageId == rewardPackage.RewardPackageId)
                .ToListAsync();
            var rewardItemIds = rewardItems
                .Select(x => x.ItemId)
                .ToHashSet();
            var items = rewardItemIds.Count == 0
                ? new Dictionary<Guid, Item>()
                : await _context.Items
                    .Where(x => rewardItemIds.Contains(x.ItemId))
                    .ToDictionaryAsync(x => x.ItemId);

            if (items.Count != rewardItemIds.Count)
            {
                throw new ConflictException("Challenge reward item is not configured");
            }

            // Lock every inventory row for this user. The user row lock serializes
            // empty-inventory claims as well, so creating a missing item is safe.
            var inventoryItems = (await _context.InventoryItems
                    .FromSqlInterpolated($$"""
                        SELECT *
                        FROM inventory_items WITH (UPDLOCK, HOLDLOCK)
                        WHERE user_id = {{userId}}
                        """)
                    .ToListAsync())
                .Where(x => rewardItemIds.Contains(x.ItemId))
                .ToDictionary(x => x.ItemId);

            foreach (var rewardItem in rewardItems)
            {
                if (inventoryItems.TryGetValue(rewardItem.ItemId, out var inventoryItem)
                    && (long)inventoryItem.Quantity + rewardItem.Quantity > int.MaxValue)
                {
                    throw new ConflictException("Inventory quantity is too large");
                }
            }

            wallet.Balance += rewardPackage.WalletAmount;

            foreach (var rewardItem in rewardItems)
            {
                if (inventoryItems.TryGetValue(rewardItem.ItemId, out var inventoryItem))
                {
                    inventoryItem.Quantity += rewardItem.Quantity;
                    continue;
                }

                inventoryItem = new InventoryItem
                {
                    UserId = userId,
                    ItemId = rewardItem.ItemId,
                    Quantity = rewardItem.Quantity
                };
                await _context.InventoryItems.AddAsync(inventoryItem);
                inventoryItems[rewardItem.ItemId] = inventoryItem;
            }

            userMission.StatusCode = ClaimedStatusCode;
            userMission.ClaimedAt = now;
            await _context.SaveChangesAsync();

            await _achievementProgressService.AddProgressAsync(
                userId,
                "mission_completed",
                1);
            await _missionProgressService.AddProgressAsync(
                userId,
                "mission_completed",
                1);

            if (rewardPackage.WalletAmount > 0)
            {
                await _achievementProgressService.AddProgressAsync(
                    userId,
                    "wallet_earned",
                    rewardPackage.WalletAmount);
                await _missionProgressService.AddProgressAsync(
                    userId,
                    "wallet_earned",
                    rewardPackage.WalletAmount);
            }

            return new ClaimPlayerChallengeRewardResponse
            {
                UserMissionId = userMission.UserMissionId,
                ChallengeId = challenge.MissionId,
                StatusCode = userMission.StatusCode,
                WalletAmount = rewardPackage.WalletAmount,
                WalletBalance = wallet.Balance,
                RewardItems = ToRewardItemResponses(rewardItems, items),
                ClaimedAt = now
            };
        });
    }

    private async Task<(UserMission UserMission, Mission Mission)?>
        GetCurrentOpenChallengeAsync(Guid userId, DateTime now)
    {
        var cycleDate = ChallengeCycleDate.FromUtc(now);
        var activeUserMissions = (await _userMissionRepository.FindAsync(x =>
                x.UserId == userId
                && x.CycleDate == cycleDate
                && x.ClaimedAt == null
                && (x.StatusCode == ActiveStatusCode
                    || x.StatusCode == CompletedStatusCode)))
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
        int cancelUsed,
        DateTime now)
    {
        return new PlayerChallengeStateResponse
        {
            CancelLimit = DailyCancelLimit,
            CancelUsed = cancelUsed,
            CancelRemaining = GetCancelRemaining(cancelUsed),
            CurrentChallenge = currentChallenge.HasValue
                ? await ToChallengeResponseAsync(
                    currentChallenge.Value.UserMission,
                    currentChallenge.Value.Mission,
                    now)
                : null
        };
    }

    private async Task<PlayerChallengeResponse> ToChallengeResponseAsync(
        UserMission userMission,
        Mission mission,
        DateTime now)
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
            IsCancelable = mission.IsCancelable
                && userMission.StatusCode == ActiveStatusCode
                && userMission.ProgressValue < mission.TargetValue,
            StatusCode = userMission.StatusCode,
            CanClaim = userMission.ClaimedAt == null
                && userMission.ProgressValue >= mission.TargetValue
                && (userMission.StatusCode == ActiveStatusCode
                    || userMission.StatusCode == CompletedStatusCode)
                && IsCurrentChallenge(mission, now),
            ClaimedAt = userMission.ClaimedAt,
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

        return ToRewardItemResponses(rewardItems, items);
    }

    private async Task LockUserAsync(Guid userId)
    {
        var userExists = await _context.Users
            .FromSqlInterpolated($$"""
                SELECT *
                FROM users WITH (UPDLOCK, HOLDLOCK)
                WHERE user_id = {{userId}}
                """)
            .AnyAsync();

        if (!userExists)
        {
            throw new NotFoundException("User not found");
        }
    }

    private Task<UserMission?> GetLockedUserMissionAsync(
        Guid userId,
        Guid userMissionId)
    {
        return _context.UserMissions
            .FromSqlInterpolated($$"""
                SELECT *
                FROM user_missions WITH (UPDLOCK, HOLDLOCK)
                WHERE user_mission_id = {{userMissionId}}
                  AND user_id = {{userId}}
                """)
            .SingleOrDefaultAsync();
    }

    private static List<PlayerChallengeRewardItemResponse> ToRewardItemResponses(
        IEnumerable<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items)
    {
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
