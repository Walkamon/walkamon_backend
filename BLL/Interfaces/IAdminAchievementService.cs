using DAL.DTO;

namespace BLL.Interfaces;

public interface IAdminAchievementService
{
    Task<AdminAchievementListResponse> GetAchievementsAsync();

    Task<AdminAchievementDetailResponse> GetAchievementDetailAsync(Guid achievementId);

    Task<AdminAchievementDetailResponse> CreateAchievementAsync(
        CreateAdminAchievementRequest request, string? iconUrl);

    Task<AdminAchievementDetailResponse> UpdateAchievementAsync(
        Guid achievementId, UpdateAdminAchievementRequest request, string? iconUrl);

    Task UpdateAchievementStatusAsync(
        Guid achievementId, UpdateAdminAchievementStatusRequest request);
}
