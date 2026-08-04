using System.Security.Cryptography;
using System.Text;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace BLL.Service;

public sealed partial class PvpSprintService
{
    private sealed record ParticipantPowerSnapshot(
        PvpPowerSnapshot Power,
        int PassiveRuleBonusBps,
        short? PassiveRuleStartMinute,
        short? PassiveRuleEndMinute);

    private sealed record PvpMatchmakingDecision(
        PvpMatchmakingPolicy Policy,
        ParticipantPowerSnapshot FirstPower,
        ParticipantPowerSnapshot SecondPower,
        string ReasonCode,
        PvpMatchQuality? HumanQuality = null,
        PvpBotDifficultyDecision? BotDifficulty = null,
        PvpBotCalibration? BotCalibration = null,
        PvpBotProfile? BotProfile = null,
        short RewardMultiplierBps = 10000,
        short BotWinMmrDelta = 0,
        short BotDrawMmrDelta = 0,
        short BotLossMmrDelta = 0);

    private async Task<ParticipantPowerSnapshot> BuildUserPowerAsync(
        Guid userId,
        PvpMatchmakingPolicy policy,
        DateTime calculatedAt,
        CancellationToken cancellationToken = default)
    {
        var vietnamDate = DateOnly.FromDateTime(AsUtc(calculatedAt).AddHours(7));
        var dailySteps = await _context.DailySteps.AsNoTracking()
            .Where(x => x.UserId == userId && x.StepDate == vietnamDate)
            .Select(x => (int?)x.EligibleStepCount)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        var affinity = await _context.UserPets.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Pet.PvpAffinityCode)
            .FirstOrDefaultAsync(cancellationToken);
        var normalizedAffinity = NormalizeAffinityCode(affinity);
        var rule = _realtimeEnabled
            ? await _context.PvpSpiritSpeedRules.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AffinityCode == normalizedAffinity && x.IsActive, cancellationToken)
            : null;
        var expectedPassive = rule != null && PvpGameplayCalculator.IsRuleActiveAtUtc(calculatedAt, rule)
            ? rule.BonusBps
            : 0;
        var loadout = _realtimeEnabled
            ? await (from slot in _context.PvpPlayerLoadoutSlots.AsNoTracking()
                     join definition in _context.PvpItemEffectDefinitions.AsNoTracking()
                         on slot.ItemId equals definition.ItemId
                     join inventory in _context.InventoryItems.AsNoTracking()
                         on new { slot.UserId, slot.ItemId } equals new { inventory.UserId, inventory.ItemId }
                     where slot.UserId == userId && definition.IsActive && inventory.Quantity > 0
                     orderby slot.SlotNo
                     select new PvpLoadoutPowerInput(
                         definition.EffectCode,
                         definition.MagnitudeBps,
                         definition.DurationMs))
                .Take(2)
                .ToListAsync(cancellationToken)
            : [];
        var power = PvpMatchPowerCalculator.Calculate(
            dailySteps,
            PvpGameplayCalculator.DefaultDailyStepPowerCap,
            PvpGameplayCalculator.DefaultMinimumPaceMilliStepsPerSecond,
            PvpGameplayCalculator.DefaultMaximumPaceMilliStepsPerSecond,
            expectedPassive,
            loadout,
            _realtimeEnabled,
            PvpMatchPowerCalculator.HumanExpectedItemUseRateBps,
            policy.MatchDurationSeconds,
            7500,
            12500,
            calculatedAt,
            normalizedAffinity);
        return new ParticipantPowerSnapshot(
            power,
            rule?.BonusBps ?? 0,
            rule == null ? null : checked((short)rule.StartMinute),
            rule == null ? null : checked((short)rule.EndMinute));
    }

    private async Task<PvpMatchmakingDecision?> BuildBotDecisionAsync(
        Guid userId,
        PvpPlayerProfile profile,
        ParticipantPowerSnapshot userPower,
        PvpMatchmakingPolicy policy,
        DateTime queuedAt,
        DateTime now,
        bool forceRelief,
        CancellationToken cancellationToken = default)
    {
        var recentBotMatches = await CountRecentBotMatchesAsync(userId, policy.BotHistoryWindow, cancellationToken);
        var roll = CalculateSelectionRoll(userId, queuedAt, policy.PolicyVersion);
        var selectionStreak = forceRelief
            ? Math.Max(profile.ConsecutiveValidRankedLosses, policy.ReliefLossThreshold)
            : Math.Min(profile.ConsecutiveValidRankedLosses, Math.Max(0, policy.ReliefLossThreshold - 1));
        var tier = PvpBotDifficultySelector.Select(
            selectionStreak,
            profile.LastBotDifficultyCode,
            profile.ConsecutiveHardBotCount,
            recentBotMatches,
            roll,
            policy);
        if (tier == null) return null;

        var profileTiers = tier.Value.IsRelief ? new[] { "easy", "relief" } : new[] { tier.Value.DifficultyCode };
        var bots = await _context.PvpBotProfiles.AsNoTracking()
            .Where(x => x.IsActive && profileTiers.Contains(x.DifficultyCode) &&
                        Math.Abs(x.Mmr - profile.Mmr) <= policy.HardMmrGap)
            .OrderBy(x => Math.Abs(x.Mmr - profile.Mmr))
            .ThenBy(x => x.BotProfileId)
            .ToListAsync(cancellationToken);
        if (bots.Count == 0)
        {
            // Existing installations may only have legacy "fair" profiles.
            // Calibration still enforces the selected tier and hard power gap;
            // keeping a profile fallback avoids stranding the queue during rollout.
            bots = await _context.PvpBotProfiles.AsNoTracking()
                .Where(x => x.IsActive && Math.Abs(x.Mmr - profile.Mmr) <= policy.HardMmrGap)
                .OrderBy(x => Math.Abs(x.Mmr - profile.Mmr))
                .ThenBy(x => x.BotProfileId)
                .ToListAsync(cancellationToken);
        }
        var targetRatio = PvpBotDifficultySelector.GetTargetBotDistanceRatioBps(tier.Value);

        foreach (var bot in bots)
        {
            var botPower = await BuildBotPowerAsync(bot, policy, now, cancellationToken);
            if (botPower.Power.ExpectedLoadoutBps > bot.ItemPowerBudgetBps)
                continue;
            if (tier.Value.DifficultyCode is "easy" or "relief" &&
                botPower.Power.ExpectedLoadoutBps > userPower.Power.ExpectedLoadoutBps)
                continue;

            var calibration = _botCalibrationService.Calibrate(
                userPower.Power,
                botPower.Power.ExpectedSpeedBps,
                PvpGameplayCalculator.ApplyAffinityPaceScale(
                    bot.MinPaceMilli,
                    NormalizeAffinityCode(bot.SpiritAffinityCode)),
                PvpGameplayCalculator.ApplyAffinityPaceScale(
                    bot.MaxPaceMilli,
                    NormalizeAffinityCode(bot.SpiritAffinityCode)),
                policy.MatchDurationSeconds,
                policy.HardPowerGapBps,
                targetRatio);
            if (calibration == null) continue;
            var minimumPace = Math.Min(
                userPower.Power.BasePaceMilliStepsPerSecond,
                calibration.Value.CalibratedPaceMilli);
            var paceRatioBps = minimumPace <= 0
                ? int.MaxValue
                : checked((int)((long)Math.Max(
                    userPower.Power.BasePaceMilliStepsPerSecond,
                    calibration.Value.CalibratedPaceMilli) * 10000 / minimumPace));
            if (paceRatioBps > policy.HardPaceRatioBps)
                continue;

            var calibratedPower = botPower with
            {
                Power = botPower.Power with
                {
                    BasePaceMilliStepsPerSecond = calibration.Value.CalibratedPaceMilli,
                    ExpectedDistanceUnits = calibration.Value.ExpectedDistanceUnits
                }
            };
            var rewardMultiplier = GetRewardMultiplier(policy, tier.Value.DifficultyCode);
            var (win, draw, loss) = GetBotRatingDeltas(policy, tier.Value.DifficultyCode);
            return new PvpMatchmakingDecision(
                policy,
                userPower,
                calibratedPower,
                tier.Value.IsRelief ? "loss_streak_relief" : "bot_fallback",
                BotDifficulty: tier,
                BotCalibration: calibration,
                BotProfile: bot,
                RewardMultiplierBps: rewardMultiplier,
                BotWinMmrDelta: win,
                BotDrawMmrDelta: draw,
                BotLossMmrDelta: loss);
        }

        return null;
    }

    private async Task<ParticipantPowerSnapshot> BuildBotPowerAsync(
        PvpBotProfile bot,
        PvpMatchmakingPolicy policy,
        DateTime calculatedAt,
        CancellationToken cancellationToken)
    {
        var affinity = NormalizeAffinityCode(bot.SpiritAffinityCode);
        var rule = _realtimeEnabled
            ? await _context.PvpSpiritSpeedRules.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AffinityCode == affinity && x.IsActive, cancellationToken)
            : null;
        var passive = rule != null && PvpGameplayCalculator.IsRuleActiveAtUtc(calculatedAt, rule)
            ? rule.BonusBps
            : 0;
        var loadout = _realtimeEnabled
            ? await (from slot in _context.PvpBotLoadoutSlots.AsNoTracking()
                     join definition in _context.PvpItemEffectDefinitions.AsNoTracking()
                         on slot.ItemId equals definition.ItemId
                     where slot.BotProfileId == bot.BotProfileId && definition.IsActive
                     orderby slot.SlotNo
                     select new PvpLoadoutPowerInput(
                         definition.EffectCode,
                         definition.MagnitudeBps,
                         definition.DurationMs))
                .Take(2)
                .ToListAsync(cancellationToken)
            : [];
        var power = PvpMatchPowerCalculator.Calculate(
            0,
            PvpGameplayCalculator.DefaultDailyStepPowerCap,
            Math.Max(1, checked((int)Math.Round(bot.StepsPerSecond * 1000m, MidpointRounding.AwayFromZero))),
            Math.Max(1, checked((int)Math.Round(bot.StepsPerSecond * 1000m, MidpointRounding.AwayFromZero))),
            passive,
            loadout,
            _realtimeEnabled,
            PvpMatchPowerCalculator.BotExpectedItemUseRateBps,
            policy.MatchDurationSeconds,
            7500,
            12500,
            calculatedAt,
            affinity);
        return new ParticipantPowerSnapshot(
            power,
            rule?.BonusBps ?? 0,
            rule == null ? null : checked((short)rule.StartMinute),
            rule == null ? null : checked((short)rule.EndMinute));
    }

    private async Task<int> CountRecentBotMatchesAsync(Guid userId, int window, CancellationToken cancellationToken)
    {
        var recent = await _context.PvpMatchPlayers.AsNoTracking()
            .Where(x => x.UserId == userId && x.Match.MatchTypeCode == "ranked" && x.Match.StatusCode == "finished")
            .OrderByDescending(x => x.Match.ResolvedAt ?? x.Match.CreatedAt)
            .Take(window)
            .Select(x => x.Match.SourceCode)
            .ToListAsync(cancellationToken);
        return recent.Count(x => x == "bot");
    }

    private static int CalculateSelectionRoll(Guid userId, DateTime queuedAt, int policyVersion)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}|{AsUtc(queuedAt).Ticks}|{policyVersion}"));
        return (int)(BitConverter.ToUInt32(bytes, 0) % 10000);
    }

    private static short GetRewardMultiplier(PvpMatchmakingPolicy policy, string tier) => tier switch
    {
        "easy" => policy.EasyRewardMultiplierBps,
        "fair" => policy.FairRewardMultiplierBps,
        "hard" => policy.HardRewardMultiplierBps,
        "relief" => policy.ReliefRewardMultiplierBps,
        _ => 10000
    };

    private static (short Win, short Draw, short Loss) GetBotRatingDeltas(PvpMatchmakingPolicy policy, string tier) => tier switch
    {
        "easy" => (policy.EasyWinMmrDelta, policy.EasyDrawMmrDelta, policy.EasyLossMmrDelta),
        "fair" => (policy.FairWinMmrDelta, policy.FairDrawMmrDelta, policy.FairLossMmrDelta),
        "hard" => (policy.HardWinMmrDelta, policy.HardDrawMmrDelta, policy.HardLossMmrDelta),
        "relief" => (policy.ReliefWinMmrDelta, policy.ReliefDrawMmrDelta, policy.ReliefLossMmrDelta),
        _ => (0, 0, 0)
    };

    private static bool IsReliefEligible(PvpPlayerProfile profile, PvpMatchmakingPolicy policy) =>
        profile.ConsecutiveValidRankedLosses >= policy.ReliefLossThreshold &&
        (!profile.LastReliefCompletedAt.HasValue ||
         profile.CompletedRankedMatchesSinceRelief >= policy.BotHistoryWindow);
}
