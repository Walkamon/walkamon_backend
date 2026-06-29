using BLL.Exceptions;
using BLL.Interfaces;
using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BLL.Service;

public class PlayerMissionService : IPlayerMissionService
{
    private const string DailyMissionTypeCode = "daily";
    private const string OverallMissionTypeCode = "overall";
    private const string ActiveStatusCode = "active";
    private const string ClaimedStatusCode = "claimed";
    private const string CancelledStatusCode = "cancelled";

    private static readonly string[] OverallMissionTypeCodes =
    [
        OverallMissionTypeCode
    ];

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<UserMission> _userMissionRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;
    private readonly IGenericRepository<Wallet> _walletRepository;
    private readonly IGenericRepository<InventoryItem> _inventoryRepository;
    private readonly WalkamonContext _context;
    private readonly IAchievementProgressService _achievementProgressService;
    private readonly IMissionProgressService _missionProgressService;

    public PlayerMissionService(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<UserMission> userMissionRepository,
        IGenericRepository<RewardPackage> rewardPackageRepository,
        IGenericRepository<RewardPackageItem> rewardPackageItemRepository,
        IGenericRepository<Item> itemRepository,
        IGenericRepository<Wallet> walletRepository,
        IGenericRepository<InventoryItem> inventoryRepository,
        WalkamonContext context,
        IAchievementProgressService achievementProgressService,
        IMissionProgressService missionProgressService)
    {
        _missionRepository = missionRepository;
        _userMissionRepository = userMissionRepository;
        _rewardPackageRepository = rewardPackageRepository;
        _rewardPackageItemRepository = rewardPackageItemRepository;
        _itemRepository = itemRepository;
        _walletRepository = walletRepository;
        _inventoryRepository = inventoryRepository;
        _context = context;
        _achievementProgressService = achievementProgressService;
        _missionProgressService = missionProgressService;
    }

    public async Task<List<PlayerMissionItemResponse>> GetDailyMissionsAsync(
        Guid userId)
    {
        return await GetMissionsAsync(userId, [DailyMissionTypeCode]);
    }

    public async Task<PlayerMissionListResponse> GetAllMissionsAsync(Guid userId)
    {
        return new PlayerMissionListResponse
        {
            DailyMissions = await GetMissionsAsync(userId, [DailyMissionTypeCode]),
            OverallMissions = await GetMissionsAsync(userId, OverallMissionTypeCodes)
        };
    }

    public async Task<ClaimMissionRewardResponse> ClaimMissionRewardAsync(
        Guid userId,
        Guid missionId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var now = DateTime.UtcNow;
            var mission = await _missionRepository.FirstOrDefaultAsync(x =>
                x.MissionId == missionId
                && (x.MissionTypeCode == DailyMissionTypeCode
                    || x.MissionTypeCode == OverallMissionTypeCode)
                && x.IsActive
                && (!x.StartAt.HasValue || x.StartAt.Value <= now)
                && (!x.EndAt.HasValue || x.EndAt.Value >= now));

            if (mission == null)
            {
                throw new NotFoundException("Mission not found");
            }

            var cycleDate = GetCycleDate(mission.MissionTypeCode, now);
            var userMission = await _context.UserMissions
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM user_missions WITH (UPDLOCK, HOLDLOCK)
                    WHERE user_id = {{userId}}
                      AND mission_id = {{missionId}}
                      AND cycle_date = {{cycleDate}}
                    """)
                .SingleOrDefaultAsync();

            if (userMission == null)
            {
                throw new BadRequestException("Mission is not completed");
            }

            if (userMission.ClaimedAt.HasValue
                || string.Equals(
                    userMission.StatusCode,
                    ClaimedStatusCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Mission reward already claimed");
            }

            if (string.Equals(
                userMission.StatusCode,
                CancelledStatusCode,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Mission is cancelled");
            }

            if (userMission.ProgressValue < mission.TargetValue)
            {
                throw new BadRequestException("Mission is not completed");
            }

            var rewardPackage = await _rewardPackageRepository.GetByIdAsync(
                mission.RewardPackageId);

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
            var items = rewardItemIds.Count == 0
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
            userMission.StatusCode = ClaimedStatusCode;
            userMission.ClaimedAt = claimedAt;
            _userMissionRepository.Update(userMission);

            await _userMissionRepository.SaveAsync();
            await transaction.CommitAsync();

            await _achievementProgressService.AddProgressAsync(userId, "mission_completed", 1);
            await _missionProgressService.AddProgressAsync(userId, "mission_completed", 1);
            if (rewardPackage.WalletAmount > 0)
            {
                await _achievementProgressService.AddProgressAsync(userId, "wallet_earned", rewardPackage.WalletAmount);
                await _missionProgressService.AddProgressAsync(userId, "wallet_earned", rewardPackage.WalletAmount);
            }

            return new ClaimMissionRewardResponse
            {
                MissionId = mission.MissionId,
                UserMissionId = userMission.UserMissionId,
                WalletAmount = rewardPackage.WalletAmount,
                RewardItems = ToRewardItemResponses(rewardItems, items),
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

    private async Task<List<PlayerMissionItemResponse>> GetMissionsAsync(
        Guid userId,
        IReadOnlyCollection<string> missionTypeCodes)
    {
        var now = DateTime.UtcNow;
        var missionsFromDb = (await _missionRepository.FindAsync(x =>
                missionTypeCodes.Contains(x.MissionTypeCode)
                && x.IsActive
                && (!x.StartAt.HasValue || x.StartAt.Value <= now)
                && (!x.EndAt.HasValue || x.EndAt.Value >= now)))
            .OrderBy(x => x.MissionTypeCode)
            .ThenBy(x => x.Title)
            .ToList();

        var missions = new List<Mission>();
        foreach (var m in missionsFromDb)
        {
            if (await _missionProgressService.ArePrerequisitesMetAsync(userId, m.MissionId))
            {
                missions.Add(m);
            }
        }

        if (missions.Count == 0)
        {
            return [];
        }

        var rewardPackageIds = missions
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
            ? []
            : (await _itemRepository.FindAsync(x =>
                    rewardItemIds.Contains(x.ItemId)))
                .ToDictionary(x => x.ItemId);

        var missionIds = missions.Select(x => x.MissionId).ToHashSet();
        var cycleDates = missions
            .Select(x => GetCycleDate(x.MissionTypeCode, now))
            .ToHashSet();
        var userMissions = (await _userMissionRepository.FindAsync(x =>
                x.UserId == userId
                && missionIds.Contains(x.MissionId)
                && cycleDates.Contains(x.CycleDate)))
            .GroupBy(x => (x.MissionId, x.CycleDate))
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(userMission => userMission.AssignedAt).First());

        return missions
            .Select(x => ToMissionItemResponse(
                x,
                rewardPackages,
                rewardItems,
                items,
                userMissions,
                now))
            .ToList();
    }

    private static PlayerMissionItemResponse ToMissionItemResponse(
        Mission mission,
        IReadOnlyDictionary<Guid, RewardPackage> rewardPackages,
        IReadOnlyCollection<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyDictionary<(Guid MissionId, DateOnly CycleDate), UserMission> userMissions,
        DateTime now)
    {
        rewardPackages.TryGetValue(mission.RewardPackageId, out var rewardPackage);
        var cycleDate = GetCycleDate(mission.MissionTypeCode, now);
        userMissions.TryGetValue((mission.MissionId, cycleDate), out var userMission);
        var missionRewardItems = rewardItems
            .Where(x => x.RewardPackageId == mission.RewardPackageId)
            .ToList();
        var statusCode = userMission?.StatusCode ?? ActiveStatusCode;
        var progressValue = userMission?.ProgressValue ?? 0;
        var canClaim = userMission != null
            && progressValue >= mission.TargetValue
            && !userMission.ClaimedAt.HasValue
            && !string.Equals(
                statusCode,
                ClaimedStatusCode,
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                statusCode,
                CancelledStatusCode,
                StringComparison.OrdinalIgnoreCase);

        return new PlayerMissionItemResponse
        {
            MissionId = mission.MissionId,
            UserMissionId = userMission?.UserMissionId,
            Title = mission.Title,
            Description = mission.Description,
            MissionTypeCode = mission.MissionTypeCode,
            StartAt = mission.StartAt,
            EndAt = mission.EndAt,
            MetricCode = mission.MetricCode,
            ProgressValue = progressValue,
            TargetValue = mission.TargetValue,
            WalletAmount = rewardPackage?.WalletAmount ?? 0,
            RewardItems = ToRewardItemResponses(missionRewardItems, items),
            StatusCode = statusCode,
            CanClaim = canClaim,
            ClaimedAt = userMission?.ClaimedAt
        };
    }

    private static List<PlayerMissionRewardItemResponse> ToRewardItemResponses(
        IReadOnlyCollection<RewardPackageItem> rewardItems,
        IReadOnlyDictionary<Guid, Item> items)
    {
        return rewardItems
            .Where(x => items.ContainsKey(x.ItemId))
            .Select(x => new PlayerMissionRewardItemResponse
            {
                ItemId = x.ItemId,
                ItemName = items[x.ItemId].ItemName,
                Quantity = x.Quantity
            })
            .ToList();
    }

    private static DateOnly GetCycleDate(string missionTypeCode, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);

        return missionTypeCode switch
        {
            DailyMissionTypeCode => today,
            OverallMissionTypeCode => DateOnly.MinValue,
            _ => today
        };
    }
}
