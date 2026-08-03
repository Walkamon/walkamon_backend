using DAL.Models;

namespace BLL.Service;

public readonly record struct PvpBotDifficultyDecision(
    string DifficultyCode,
    bool IsRelief,
    int SelectionRollBps,
    int TargetUserWinBps,
    string ReasonCode);

public static class PvpBotDifficultySelector
{
    public static PvpBotDifficultyDecision? Select(
        int validLossStreak,
        string? lastBotDifficultyCode,
        int consecutiveHardBots,
        int recentBotMatches,
        int selectionRollBps,
        PvpMatchmakingPolicy policy)
    {
        selectionRollBps = Math.Clamp(selectionRollBps, 0, 9999);
        if (validLossStreak >= policy.ReliefLossThreshold)
            return new("relief", true, selectionRollBps, policy.ReliefTargetUserWinBps, "loss_streak_relief");

        if (recentBotMatches >= policy.MaxBotMatchesInWindow)
            return null;

        var (easy, fair, hard) = validLossStreak switch
        {
            <= 1 => (policy.Streak01EasyWeightBps, policy.Streak01FairWeightBps, policy.Streak01HardWeightBps),
            <= 3 => (policy.Streak23EasyWeightBps, policy.Streak23FairWeightBps, policy.Streak23HardWeightBps),
            _ => (policy.Streak4EasyWeightBps, policy.Streak4FairWeightBps, policy.Streak4HardWeightBps)
        };

        if (!policy.AllowConsecutiveHard &&
            (string.Equals(lastBotDifficultyCode, "hard", StringComparison.OrdinalIgnoreCase) || consecutiveHardBots > 0))
        {
            hard = 0;
        }

        var total = easy + fair + hard;
        if (total <= 0) return null;
        var normalizedRoll = selectionRollBps * total / 10000;
        if (normalizedRoll < easy)
            return new("easy", false, selectionRollBps, policy.EasyTargetUserWinBps, "weighted_fallback");
        if (normalizedRoll < easy + fair)
            return new("fair", false, selectionRollBps, policy.FairTargetUserWinBps, "weighted_fallback");
        return new("hard", false, selectionRollBps, policy.HardTargetUserWinBps, "weighted_fallback");
    }

    public static int GetTargetBotDistanceRatioBps(PvpBotDifficultyDecision decision)
    {
        var favorable = decision.SelectionRollBps < decision.TargetUserWinBps;
        return decision.DifficultyCode switch
        {
            "relief" or "easy" => favorable
                ? Interpolate(9200, 9900, decision.SelectionRollBps, Math.Max(1, decision.TargetUserWinBps))
                : Interpolate(10050, 10200, decision.SelectionRollBps - decision.TargetUserWinBps, Math.Max(1, 10000 - decision.TargetUserWinBps)),
            "fair" => favorable
                ? Interpolate(9700, 9950, decision.SelectionRollBps, Math.Max(1, decision.TargetUserWinBps))
                : Interpolate(10050, 10300, decision.SelectionRollBps - decision.TargetUserWinBps, Math.Max(1, 10000 - decision.TargetUserWinBps)),
            "hard" => favorable
                ? Interpolate(9700, 9950, decision.SelectionRollBps, Math.Max(1, decision.TargetUserWinBps))
                : Interpolate(10050, 10800, decision.SelectionRollBps - decision.TargetUserWinBps, Math.Max(1, 10000 - decision.TargetUserWinBps)),
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };
    }

    private static int Interpolate(int minimum, int maximum, int value, int range) =>
        minimum + checked((int)((long)(maximum - minimum) * Math.Clamp(value, 0, range) / range));
}
