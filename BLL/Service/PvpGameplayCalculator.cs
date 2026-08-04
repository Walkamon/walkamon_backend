using DAL.Models;

namespace BLL.Service;

public static class PvpGameplayCalculator
{
    public const int BaseSpeedBps = 10000;
    public const int DistanceUnitsPerStep = 10000;
    public const int DefaultDailyStepPowerCap = 10000;
    public const int DefaultMinimumPaceMilliStepsPerSecond = 1000;
    public const int DefaultMaximumPaceMilliStepsPerSecond = 2500;
    public const int SproutPaceScaleNumerator = 2;
    public const int SproutPaceScaleDenominator = 3;

    public static int CalculateDailyPowerPaceMilli(
        int eligibleDailySteps,
        int dailyStepPowerCap = DefaultDailyStepPowerCap,
        int minimumPaceMilli = DefaultMinimumPaceMilliStepsPerSecond,
        int maximumPaceMilli = DefaultMaximumPaceMilliStepsPerSecond,
        string? affinityCode = null)
    {
        if (dailyStepPowerCap <= 0)
            throw new ArgumentOutOfRangeException(nameof(dailyStepPowerCap));
        if (minimumPaceMilli <= 0 || maximumPaceMilli < minimumPaceMilli)
            throw new ArgumentOutOfRangeException(nameof(maximumPaceMilli));

        var cappedSteps = Math.Clamp(eligibleDailySteps, 0, dailyStepPowerCap);
        var paceRange = maximumPaceMilli - minimumPaceMilli;
        var scaled = (decimal)cappedSteps * paceRange / dailyStepPowerCap;
        var pace = checked(minimumPaceMilli +
                           (int)Math.Round(scaled, MidpointRounding.AwayFromZero));
        return ApplyAffinityPaceScale(pace, affinityCode);
    }

    public static int ApplyAffinityPaceScale(int paceMilli, string? affinityCode)
    {
        if (paceMilli <= 0) return paceMilli;
        if (!string.Equals(affinityCode, "sprout", StringComparison.OrdinalIgnoreCase))
            return paceMilli;

        return checked((int)Math.Round(
            paceMilli * (decimal)SproutPaceScaleNumerator / SproutPaceScaleDenominator,
            MidpointRounding.AwayFromZero));
    }

    public static long CalculatePacedDistanceUnits(
        TimeSpan duration,
        int paceMilliStepsPerSecond,
        int multiplierBps)
    {
        if (duration <= TimeSpan.Zero || paceMilliStepsPerSecond <= 0)
            return 0;

        var distance = (decimal)duration.TotalSeconds *
                       paceMilliStepsPerSecond *
                       multiplierBps /
                       1000m;
        return checked((long)Math.Round(distance, MidpointRounding.AwayFromZero));
    }

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

    public static long CalculateDistanceUnits(
        int eligibleSteps,
        int multiplierBps,
        string? affinityCode = null)
    {
        var distance = checked((long)eligibleSteps * multiplierBps);
        return string.Equals(affinityCode, "sprout", StringComparison.OrdinalIgnoreCase)
            ? checked((long)Math.Round(
                distance * (decimal)SproutPaceScaleNumerator / SproutPaceScaleDenominator,
                MidpointRounding.AwayFromZero))
            : distance;
    }

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
