using DAL.DTO;

namespace BLL.Interfaces;

public interface IAdminMissionService
{
    Task<AdminMissionListResponse> GetDailyMissionsAsync();

    Task<AdminMissionDetailResponse> GetDailyMissionDetailAsync(Guid missionId);

    Task<AdminMissionDetailResponse> CreateDailyMissionAsync(
        CreateAdminMissionRequest request);

    Task<AdminMissionDetailResponse> UpdateDailyMissionAsync(
        Guid missionId,
        UpdateAdminMissionRequest request);

    Task UpdateDailyMissionStatusAsync(
        Guid missionId,
        UpdateAdminMissionStatusRequest request);

    Task<AdminMissionListResponse> GetOverallMissionsAsync();

    Task<AdminMissionDetailResponse> GetOverallMissionDetailAsync(Guid missionId);

    Task<AdminMissionDetailResponse> CreateOverallMissionAsync(
        CreateAdminMissionRequest request);

    Task<AdminMissionDetailResponse> UpdateOverallMissionAsync(
        Guid missionId,
        UpdateAdminMissionRequest request);

    Task UpdateOverallMissionStatusAsync(
        Guid missionId,
        UpdateAdminMissionStatusRequest request);
}
