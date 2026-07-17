using DAL.Models;

namespace BLL.Service;

public static class PvpGameplayCalculator
{
    public const int BaseSpeedBps = 10000;
    public const int DistanceUnitsPerStep = 10000;

    public static int CalculateSpeedBps(
        int passiveBonusBps,
        IEnumerable<(string Kind, int MagnitudeBps)> activeEffects,
        int minimumBps = 7500,
        int maximumBps = 12500)
    {
        var speed = BaseSpeedBps + passiveBonusBps;
        foreach (var effect in activeEffects)
        {
            speed += effect.Kind == "debuff" ? -effect.MagnitudeBps : effect.Kind == "buff" ? effect.MagnitudeBps : 0;
        }

        return Math.Clamp(speed, minimumBps, maximumBps);
    }

    public static long CalculateDistanceUnits(int eligibleSteps, int multiplierBps) =>
        checked((long)eligibleSteps * multiplierBps);

    public static bool IsRuleActiveAtUtc(DateTime utc, PvpSpiritSpeedRule rule)
    {
        if (!rule.IsActive || rule.BonusBps <= 0) return false;
        var vietnam = utc.ToUniversalTime().AddHours(7);
        var minute = vietnam.Hour * 60 + vietnam.Minute;
        return rule.StartMinute <= rule.EndMinute
            ? minute >= rule.StartMinute && minute <= rule.EndMinute
            : minute >= rule.StartMinute || minute <= rule.EndMinute;
    }

    public static PvpRankTier ResolveTier(int mmr, IEnumerable<PvpRankTier> tiers) =>
        tiers.Where(x => x.IsActive && x.MinMmr <= mmr)
            .OrderByDescending(x => x.MinMmr)
            .FirstOrDefault()
        ?? throw new InvalidOperationException("PvP rank tier configuration does not cover this MMR.");
}
