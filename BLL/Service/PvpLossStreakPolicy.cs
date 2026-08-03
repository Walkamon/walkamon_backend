namespace BLL.Service;

public readonly record struct PvpCompletionEvidence(
    bool IsRanked,
    bool IsRelief,
    bool IsNormalCompletion,
    bool WasReady,
    bool JoinedRealtime,
    string ResultCode);

public readonly record struct PvpLossStreakDecision(
    bool IsEligible,
    int NewLossStreak,
    bool ResetByRelief,
    string EligibilityCode);

public static class PvpLossStreakPolicy
{
    public static PvpLossStreakDecision Evaluate(int currentLossStreak, PvpCompletionEvidence evidence)
    {
        if (!evidence.IsRanked)
            return new(false, currentLossStreak, false, "not_ranked");
        if (!evidence.IsNormalCompletion)
            return new(false, currentLossStreak, false, "not_normal_completion");
        if (!evidence.WasReady)
            return new(false, currentLossStreak, false, "not_ready");
        if (!evidence.JoinedRealtime)
            return new(false, currentLossStreak, false, "realtime_not_joined");
        if (evidence.IsRelief)
            return new(true, 0, true, "relief_completed");

        return evidence.ResultCode switch
        {
            "lose" => new(true, checked(currentLossStreak + 1), false, "valid_loss"),
            "win" => new(true, 0, false, "valid_win"),
            "draw" => new(true, 0, false, "valid_draw"),
            _ => new(false, currentLossStreak, false, "invalid_result")
        };
    }
}
