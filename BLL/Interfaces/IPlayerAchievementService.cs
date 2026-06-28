using DAL.DTO;

namespace BLL.Interfaces;

public interface IPlayerAchievementService
{
    Task<List<PlayerAchievementItemResponse>> GetAchievementsAsync(Guid userId);
    
    Task<PlayerAchievementItemResponse> GetAchievementDetailAsync(Guid userId, Guid achievementId);

    Task<ClaimAchievementRewardResponse> ClaimAchievementRewardAsync(
        Guid userId, Guid achievementId);
}
