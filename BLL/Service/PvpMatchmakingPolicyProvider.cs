using BLL.Exceptions;
using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace BLL.Service;

public sealed class PvpMatchmakingPolicyProvider
{
    private readonly WalkamonContext _context;

    public PvpMatchmakingPolicyProvider(WalkamonContext context) => _context = context;

    public async Task<PvpMatchmakingPolicy> GetActivePolicyAsync(CancellationToken cancellationToken = default)
    {
        var policy = await _context.PvpMatchmakingPolicies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IsActive, cancellationToken)
            ?? throw new ConflictException("An active PvP matchmaking policy is required.");
        Validate(policy);
        return policy;
    }

    public static PvpMatchmakingPolicy CreateDefault() => new()
    {
        PolicyVersion = 1,
        IsActive = true,
        CreatedAt = DateTime.UnixEpoch,
        ActivatedAt = DateTime.UnixEpoch
    };

    public static void Validate(PvpMatchmakingPolicy policy)
    {
        if (policy.MatchDurationSeconds is < 10 or > 120 || policy.BotFallbackSeconds is < 1 or > 120)
            throw new ConflictException("PvP matchmaking timing policy is invalid.");
        if (policy.Stage1MmrGap < 0 || policy.Stage1PowerGapBps < 0 ||
            policy.Stage1PaceRatioBps < 10000 || policy.HardMmrGap <= 0 ||
            policy.HardPowerGapBps <= 0 || policy.HardPaceRatioBps < 10000)
            throw new ConflictException("PvP matchmaking limits are invalid.");
        if (policy.Stage1MmrGap > policy.Stage2MmrGap || policy.Stage2MmrGap > policy.Stage3MmrGap || policy.Stage3MmrGap > policy.HardMmrGap)
            throw new ConflictException("PvP matchmaking MMR windows are invalid.");
        if (policy.Stage1PowerGapBps > policy.Stage2PowerGapBps || policy.Stage2PowerGapBps > policy.Stage3PowerGapBps || policy.Stage3PowerGapBps > policy.HardPowerGapBps)
            throw new ConflictException("PvP matchmaking power windows are invalid.");
        if (policy.Stage1PaceRatioBps > policy.Stage2PaceRatioBps || policy.Stage2PaceRatioBps > policy.Stage3PaceRatioBps || policy.Stage3PaceRatioBps > policy.HardPaceRatioBps)
            throw new ConflictException("PvP matchmaking pace windows are invalid.");
        ValidateWeights(policy.Streak01EasyWeightBps, policy.Streak01FairWeightBps, policy.Streak01HardWeightBps);
        ValidateWeights(policy.Streak23EasyWeightBps, policy.Streak23FairWeightBps, policy.Streak23HardWeightBps);
        ValidateWeights(policy.Streak4EasyWeightBps, policy.Streak4FairWeightBps, policy.Streak4HardWeightBps);
        if (policy.ReliefLossThreshold <= 0 || policy.MaxBotMatchesInWindow > policy.BotHistoryWindow)
            throw new ConflictException("PvP protection policy is invalid.");
        if (policy.BotRatingWindow <= 0 || policy.MaxPositiveBotMmrInWindow < 0)
            throw new ConflictException("PvP bot rating policy is invalid.");
        foreach (var value in new[]
                 {
                     policy.ReliefTargetUserWinBps, policy.EasyTargetUserWinBps,
                     policy.FairTargetUserWinBps, policy.HardTargetUserWinBps,
                     policy.EasyRewardMultiplierBps, policy.FairRewardMultiplierBps,
                     policy.HardRewardMultiplierBps, policy.ReliefRewardMultiplierBps
                 })
        {
            if (value is < 0 or > 10000)
                throw new ConflictException("PvP probability or reward policy is invalid.");
        }
    }

    private static void ValidateWeights(int easy, int fair, int hard)
    {
        if (easy < 0 || fair < 0 || hard < 0 || easy + fair + hard != 10000)
            throw new ConflictException("PvP bot difficulty weights must add up to 10000 BPS.");
    }
}
