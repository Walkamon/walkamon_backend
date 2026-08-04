using DAL.DTO;

namespace BLL.Interfaces;

public interface IPlayerChallengeService
{
    Task<PlayerChallengeStateResponse> GetRandomChallengeStateAsync(Guid userId);

    Task<PlayerChallengeStateResponse> CreateRandomChallengeAsync(Guid userId);

    Task<CancelPlayerChallengeResponse> CancelChallengeAsync(
        Guid userId,
        Guid userMissionId);

    Task<ClaimPlayerChallengeRewardResponse> ClaimChallengeRewardAsync(
        Guid userId,
        Guid userMissionId);
}
