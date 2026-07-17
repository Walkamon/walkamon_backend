using DAL.DTO;

namespace BLL.Interfaces;

public interface IValidatedStepService
{
    Task<PvpStepSessionResponse> CreateDailySessionAsync(
        Guid userId,
        CreatePvpStepSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<PvpStepSessionResponse> CreatePvpSessionAsync(
        Guid userId,
        Guid matchId,
        CreatePvpStepSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<PvpStepBatchResponse> SubmitDailyBatchAsync(
        Guid userId,
        Guid sessionId,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<PvpStepBatchResponse> SubmitPvpBatchAsync(
        Guid userId,
        Guid matchId,
        Guid sessionId,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken = default);
}
