using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace BLL.Service;

public class PlayerMissionService : IPlayerMissionService
{
    private const string DailyMissionTypeCode = "daily";
    private const string OverallMissionTypeCode = "overall";
    private const string ActiveStatusCode = "active";

    private static readonly string[] OverallMissionTypeCodes =
    [
        OverallMissionTypeCode
    ];

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<UserMission> _userMissionRepository;
    private readonly IGenericRepository<RewardPackage> _rewardPackageRepository;
    private readonly IGenericRepository<RewardPackageItem> _rewardPackageItemRepository;
    private readonly IGenericRepository<Item> _itemRepository;

    public PlayerMissionService(
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

    private async Task<List<PlayerMissionItemResponse>> GetMissionsAsync(
        Guid userId,
        IReadOnlyCollection<string> missionTypeCodes)
    {
        var now = DateTime.UtcNow;
        var missions = (await _missionRepository.FindAsync(x =>
                missionTypeCodes.Contains(x.MissionTypeCode)
                && x.IsActive
                && (!x.StartAt.HasValue || x.StartAt.Value <= now)
                && (!x.EndAt.HasValue || x.EndAt.Value >= now)))
            .OrderBy(x => x.MissionTypeCode)
            .ThenBy(x => x.Title)
            .ToList();

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

        return new PlayerMissionItemResponse
        {
            MissionId = mission.MissionId,
            UserMissionId = userMission?.UserMissionId,
            Title = mission.Title,
            Description = mission.Description,
            MissionTypeCode = mission.MissionTypeCode,
            MetricCode = mission.MetricCode,
            ProgressValue = progressValue,
            TargetValue = mission.TargetValue,
            WalletAmount = rewardPackage?.WalletAmount ?? 0,
            RewardItems = ToRewardItemResponses(missionRewardItems, items),
            StatusCode = statusCode
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
