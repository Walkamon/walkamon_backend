namespace BLL.Service;

public readonly record struct PvpBotCalibration(
    int CalibratedPaceMilli,
    int ExpectedSpeedBps,
    long ExpectedDistanceUnits,
    int ExpectedGapBps,
    int TargetDistanceRatioBps);

public sealed class PvpBotCalibrationService
{
    public PvpBotCalibration? Calibrate(
        PvpPowerSnapshot userPower,
        int botExpectedSpeedBps,
        int botMinimumPaceMilli,
        int botMaximumPaceMilli,
        int matchDurationSeconds,
        int hardPowerGapBps,
        int targetDistanceRatioBps)
    {
        if (botExpectedSpeedBps <= 0 || botMinimumPaceMilli <= 0 || botMaximumPaceMilli < botMinimumPaceMilli)
            return null;

        var targetDistance = checked(userPower.ExpectedDistanceUnits * targetDistanceRatioBps / 10000L);
        var denominator = checked((long)matchDurationSeconds * botExpectedSpeedBps);
        if (denominator <= 0) return null;
        var rawPace = checked((int)Math.Round(
            targetDistance * 1000m / denominator,
            MidpointRounding.AwayFromZero));
        var pace = Math.Clamp(rawPace, botMinimumPaceMilli, botMaximumPaceMilli);
        var distance = PvpGameplayCalculator.CalculatePacedDistanceUnits(
            TimeSpan.FromSeconds(matchDurationSeconds), pace, botExpectedSpeedBps);
        var gap = PvpMatchQualityEvaluator.CalculatePowerGapBps(userPower.ExpectedDistanceUnits, distance);
        if (gap > hardPowerGapBps) return null;

        return new(pace, botExpectedSpeedBps, distance, gap, targetDistanceRatioBps);
    }
}
