namespace BLL.Service;

public readonly record struct PvpLoadoutPowerInput(
    string EffectCode,
    int MagnitudeBps,
    int DurationMs);

public readonly record struct PvpPowerSnapshot(
    int DailyEligibleSteps,
    int BasePaceMilliStepsPerSecond,
    int ExpectedPassiveBps,
    int ExpectedLoadoutBps,
    int ExpectedSpeedBps,
    long ExpectedDistanceUnits,
    DateTime CalculatedAt);

public static class PvpMatchPowerCalculator
{
    public const int HumanExpectedItemUseRateBps = 7000;
    public const int BotExpectedItemUseRateBps = 10000;

    public static PvpPowerSnapshot Calculate(
        int dailyEligibleSteps,
        int dailyStepPowerCap,
        int minimumPaceMilli,
        int maximumPaceMilli,
        int expectedPassiveBps,
        IEnumerable<PvpLoadoutPowerInput> loadout,
        bool realtimeEnabled,
        int expectedItemUseRateBps,
        int matchDurationSeconds,
        int speedMinimumBps,
        int speedMaximumBps,
        DateTime calculatedAt,
        string? affinityCode = null)
    {
        if (matchDurationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchDurationSeconds));
        if (expectedItemUseRateBps is < 0 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(expectedItemUseRateBps));

        var pace = PvpGameplayCalculator.CalculateDailyPowerPaceMilli(
            dailyEligibleSteps,
            dailyStepPowerCap,
            minimumPaceMilli,
            maximumPaceMilli,
            affinityCode);
        var passive = realtimeEnabled ? Math.Max(0, expectedPassiveBps) : 0;
        var loadoutBps = realtimeEnabled
            ? CalculateExpectedLoadoutBps(loadout, expectedItemUseRateBps, matchDurationSeconds)
            : 0;
        var speed = Math.Clamp(
            PvpGameplayCalculator.BaseSpeedBps + passive + loadoutBps,
            speedMinimumBps,
            speedMaximumBps);
        var distance = PvpGameplayCalculator.CalculatePacedDistanceUnits(
            TimeSpan.FromSeconds(matchDurationSeconds),
            pace,
            speed);

        return new PvpPowerSnapshot(
            Math.Max(0, dailyEligibleSteps),
            pace,
            passive,
            loadoutBps,
            speed,
            distance,
            calculatedAt);
    }

    public static int CalculateExpectedLoadoutBps(
        IEnumerable<PvpLoadoutPowerInput> loadout,
        int expectedItemUseRateBps,
        int matchDurationSeconds)
    {
        if (matchDurationSeconds <= 0) return 0;

        long total = 0;
        foreach (var item in loadout)
        {
            // Speed-up increases the owner's distance. Speed-down is treated as
            // equivalent offensive pressure for matchmaking. Shield and cleanse
            // are deliberately valued at zero until telemetry can calibrate a
            // reliable mitigation value.
            if (item.EffectCode is not ("pvp_speed_up" or "pvp_speed_down"))
                continue;

            var effectiveDurationMs = Math.Clamp(
                item.DurationMs,
                0,
                checked(matchDurationSeconds * 1000));
            total += (long)Math.Max(0, item.MagnitudeBps) *
                     effectiveDurationMs *
                     expectedItemUseRateBps /
                     checked(matchDurationSeconds * 1000L * 10000L);
        }

        return checked((int)Math.Min(int.MaxValue, total));
    }
}
