using DAL.DTO;

namespace BLL.Interfaces;

public interface IAdminChallengeService
{
    Task<AdminChallengeListResponse> GetChallengesAsync(
        string? search,
        string? status);

    Task<AdminChallengeDetailResponse> GetChallengeDetailAsync(Guid challengeId);

    Task<AdminChallengeDetailResponse> CreateChallengeAsync(
        CreateAdminChallengeRequest request);

    Task<AdminChallengeDetailResponse> UpdateChallengeAsync(
        Guid challengeId,
        UpdateAdminChallengeRequest request);

    Task UpdateChallengeStatusAsync(
        Guid challengeId,
        UpdateAdminChallengeStatusRequest request);
}
