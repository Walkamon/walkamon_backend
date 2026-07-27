using DAL.DTO;

namespace BLL.Interfaces;

public interface IPvpSprintService
{
    Task<PvpInviteResponse> CreateInviteAsync(Guid userId, CreatePvpSprintInviteRequest request);
    Task<PvpInviteResponse> RespondInviteAsync(Guid userId, Guid inviteId, RespondPvpSprintInviteRequest request);
    Task CancelInviteAsync(Guid userId, Guid inviteId);
    Task<PvpPagedResponse<PvpInviteResponse>> GetInvitesAsync(Guid userId, string direction, string? status, int page, int pageSize);
    Task<PvpMatchmakingStatusResponse> JoinMatchmakingAsync(Guid userId, JoinPvpMatchmakingRequest request);
    Task<PvpMatchmakingStatusResponse> GetMatchmakingStatusAsync(Guid userId);
    Task CancelMatchmakingAsync(Guid userId);
    Task<PvpMatchResponse> GetMatchAsync(Guid userId, Guid matchId);
    Task<PvpMatchReadyResponse> ReadyMatchAsync(Guid userId, Guid matchId);
    Task<PvpResultResponse> GetResultAsync(Guid userId, Guid matchId);
    Task<PvpPagedResponse<PvpMatchResponse>> GetHistoryAsync(Guid userId, int page, int pageSize, string? matchType, string? result, DateTime? from, DateTime? to, bool includeActive);
    Task<PvpStepSessionResponse> CreateStepSessionAsync(Guid userId, Guid matchId, CreatePvpStepSessionRequest request);
    Task<PvpStepBatchResponse> SubmitStepBatchAsync(Guid userId, Guid matchId, Guid sessionId, SubmitPvpStepBatchRequest request);
    Task<PvpRewardClaimResponse> ClaimRewardAsync(Guid userId, Guid matchId);
    Task<PvpLoadoutResponse> GetLoadoutAsync(Guid userId);
    Task<PvpLoadoutResponse> UpdateLoadoutAsync(Guid userId, UpdatePvpLoadoutRequest request);
    Task<UsePvpItemResponse> UseItemAsync(Guid userId, Guid matchId, UsePvpItemRequest request);
    Task<PvpProfileResponse> GetProfileAsync(Guid userId);
    Task<PvpPagedResponse<PvpRankingEntryResponse>> GetRankingsAsync(Guid userId, int page, int pageSize);
    Task<List<PvpRewardRuleResponse>> GetRewardRulesAsync();
    Task UpdateRewardRulesAsync(UpdatePvpRewardRulesRequest request);
    Task<List<PvpItemEffectAdminRequest>> GetItemEffectsAsync();
    Task UpdateItemEffectsAsync(UpdatePvpItemEffectsRequest request);
    Task<List<PvpSpiritRuleAdminRequest>> GetSpiritRulesAsync();
    Task UpdateSpiritRulesAsync(UpdatePvpSpiritRulesRequest request);
    Task<List<PvpRankTierAdminRequest>> GetRankTiersAsync();
    Task UpdateRankTiersAsync(UpdatePvpRankTiersRequest request);
    Task ProcessDueWorkAsync(CancellationToken cancellationToken = default);
}
