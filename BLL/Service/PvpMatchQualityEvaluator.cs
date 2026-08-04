using DAL.Models;

namespace BLL.Service;

public readonly record struct PvpMatchQuality(
    bool IsEligible,
    string ReasonCode,
    int MmrGap,
    int PowerGapBps,
    int PaceRatioBps,
    int QualityCost);

public static class PvpMatchQualityEvaluator
{
    public static PvpMatchQuality Evaluate(
        int firstMmr,
        PvpPowerSnapshot firstPower,
        TimeSpan firstWait,
        int secondMmr,
        PvpPowerSnapshot secondPower,
        TimeSpan secondWait,
        PvpMatchmakingPolicy policy)
    {
        var mmrGap = Math.Abs(firstMmr - secondMmr);
        var maxDistance = Math.Max(firstPower.ExpectedDistanceUnits, secondPower.ExpectedDistanceUnits);
        var powerGap = maxDistance == 0
            ? 0
            : checked((int)Math.Min(10000,
                Math.Abs(firstPower.ExpectedDistanceUnits - secondPower.ExpectedDistanceUnits) * 10000L / maxDistance));
        var minimumPace = Math.Min(firstPower.BasePaceMilliStepsPerSecond, secondPower.BasePaceMilliStepsPerSecond);
        var paceRatio = minimumPace <= 0
            ? int.MaxValue
            : checked((int)((long)Math.Max(firstPower.BasePaceMilliStepsPerSecond, secondPower.BasePaceMilliStepsPerSecond) * 10000 / minimumPace));

        if (mmrGap > policy.HardMmrGap)
            return Reject("hard_mmr_gap", mmrGap, powerGap, paceRatio);
        if (powerGap > policy.HardPowerGapBps)
            return Reject("hard_power_gap", mmrGap, powerGap, paceRatio);
        if (paceRatio > policy.HardPaceRatioBps)
            return Reject("hard_pace_ratio", mmrGap, powerGap, paceRatio);

        var firstWindow = ResolveWindow(firstWait, policy);
        var secondWindow = ResolveWindow(secondWait, policy);
        var mmrLimit = Math.Min(firstWindow.MmrGap, secondWindow.MmrGap);
        var powerLimit = Math.Min(firstWindow.PowerGapBps, secondWindow.PowerGapBps);
        var paceLimit = Math.Min(firstWindow.PaceRatioBps, secondWindow.PaceRatioBps);

        if (mmrGap > mmrLimit)
            return Reject("soft_mmr_gap", mmrGap, powerGap, paceRatio);
        if (powerGap > powerLimit)
            return Reject("soft_power_gap", mmrGap, powerGap, paceRatio);
        if (paceRatio > paceLimit)
            return Reject("soft_pace_ratio", mmrGap, powerGap, paceRatio);

        var cost = Normalize(mmrGap, policy.HardMmrGap, 3000) +
                   Normalize(powerGap, policy.HardPowerGapBps, 4500) +
                   Normalize(Math.Max(0, paceRatio - 10000), Math.Max(1, policy.HardPaceRatioBps - 10000), 2500);
        return new PvpMatchQuality(true, "accepted", mmrGap, powerGap, paceRatio, cost);
    }

    public static int CalculatePowerGapBps(long firstDistance, long secondDistance)
    {
        var maximum = Math.Max(firstDistance, secondDistance);
        return maximum <= 0
            ? 0
            : checked((int)Math.Min(10000, Math.Abs(firstDistance - secondDistance) * 10000L / maximum));
    }

    private static (int MmrGap, int PowerGapBps, int PaceRatioBps) ResolveWindow(
        TimeSpan wait,
        PvpMatchmakingPolicy policy) => wait.TotalSeconds switch
        {
            < 5 => (policy.Stage1MmrGap, policy.Stage1PowerGapBps, policy.Stage1PaceRatioBps),
            < 10 => (policy.Stage2MmrGap, policy.Stage2PowerGapBps, policy.Stage2PaceRatioBps),
            _ => (policy.Stage3MmrGap, policy.Stage3PowerGapBps, policy.Stage3PaceRatioBps)
        };

    private static int Normalize(int value, int limit, int weight) =>
        limit <= 0 ? weight : checked((int)Math.Min(weight, (long)value * weight / limit));

    private static PvpMatchQuality Reject(string reason, int mmr, int power, int pace) =>
        new(false, reason, mmr, power, pace, int.MaxValue);
}
