using DAL.DTO;

namespace BLL.Interfaces;

public interface IPlayerMissionService
{
    Task<List<PlayerMissionItemResponse>> GetDailyMissionsAsync(Guid userId);

    Task<PlayerMissionListResponse> GetAllMissionsAsync(Guid userId);

    Task<ClaimMissionRewardResponse> ClaimMissionRewardAsync(
        Guid userId,
        Guid missionId);
}
