using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BLL.Service;

public static class SimpleTemporalEvidenceClasses
{
    public const string NoClearFraud = "NO_CLEAR_FRAUD";
    public const string FraudRegionPresent = "FRAUD_REGION_PRESENT";
    public const string MixedOrUncertain = "MIXED_OR_UNCERTAIN";
    public const string InsufficientEvidence = "INSUFFICIENT_EVIDENCE";
}

public static class TemporalFraudTypes
{
    public const string HardShake = "HARD_SHAKE";
}

public sealed record TemporalMotionEvidenceWindow(
    Guid MotionWindowId,
    Guid BootSessionId,
    long WindowStartElapsedNs,
    long WindowEndElapsedNs,
    string Classification,
    IReadOnlyList<string> ReasonCodes,
    string ActivityCode,
    int ActivityConfidence,
    int SampleCount = 0,
    bool IsCurrentEvidence = false);

public sealed record TemporalFraudRegion(
    Guid BootSessionId,
    long StartElapsedNs,
    long EndElapsedNs,
    string FraudType,
    IReadOnlyList<string> SupportingReasonCodes,
    IReadOnlyList<Guid> MotionWindowIds,
    bool HardShakeEvidence,
    IReadOnlyList<string> ActivityContext)
{
    public long DurationNs => Math.Max(0, EndElapsedNs - StartElapsedNs);
}

public sealed record TemporalFraudEvaluationInput(
    Guid SessionId,
    SimpleCounterInterval Interval,
    int DetectorCount,
    IReadOnlyList<TemporalMotionEvidenceWindow> MotionWindows);

public sealed record TemporalFraudEvaluation(
    Guid SessionId,
    Guid BootSessionId,
    string CounterIntervalId,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterDelta,
    int DetectorCount,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    int FraudRegionCount,
    long FraudDurationMs,
    long IntervalDurationMs,
    decimal FraudCoverageRatio,
    int HardShakeRegionCount,
    long MaxFraudRegionDurationMs,
    long ShadowCounterCandidate,
    IReadOnlyList<SimpleStepActivityDistribution> ActivityDistribution,
    IReadOnlyList<TemporalFraudRegion> FraudRegions,
    string SimpleV2EvidenceClass,
    string EvidenceFingerprint);

public static class TemporalFraudRegionEvaluator
{
    private const string GyroscopeShakeReason = "gyroscope_shake_pattern";
    private const string AccelerationShakeReason = "acceleration_shake_pattern";
    private const string HardShakeReason = "hard_shake_majority";

    public static TemporalFraudEvaluation Evaluate(TemporalFraudEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var interval = input.Interval;
        var windows = Canonicalize(input).ToArray();
        var atomicFraud = windows
            .Select(window => (Window: window, FraudType: FraudType(window)))
            .Where(x => x.FraudType != null)
            .Select(x => (x.Window, FraudType: x.FraudType!))
            .OrderBy(x => x.Window.WindowStartElapsedNs)
            .ThenBy(x => x.Window.WindowEndElapsedNs)
            .ThenBy(x => x.Window.MotionWindowId)
            .ToArray();
        var regions = BuildRegions(atomicFraud).ToArray();
        var intervalDurationNs = Math.Max(
            0,
            interval.IntervalEndElapsedNs - interval.IntervalStartElapsedNs);
        var fraudDurationNs = UnionDurationNs(regions);
        var motionAccepted = Count(windows, "accepted");
        var motionSuspicious = Count(windows, "suspicious");
        var motionRejected = Count(windows, "rejected");
        var motionUnavailable = windows.Length -
            motionAccepted - motionSuspicious - motionRejected;
        var hasUncertainEvidence = windows.Any(window =>
            NormalizeStatus(window.Classification) is "suspicious" or "rejected" ||
            HasExactlyOneShakeSignal(window.ReasonCodes));
        var evidenceClass = regions.Length > 0
            ? SimpleTemporalEvidenceClasses.FraudRegionPresent
            : windows.Length == 0
                ? SimpleTemporalEvidenceClasses.InsufficientEvidence
                : hasUncertainEvidence
                    ? SimpleTemporalEvidenceClasses.MixedOrUncertain
                    : SimpleTemporalEvidenceClasses.NoClearFraud;
        var activities = windows
            .GroupBy(x => NormalizeActivity(x.ActivityCode))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var confidence = group
                    .Select(x => Math.Clamp(x.ActivityConfidence, 0, 100))
                    .ToArray();
                return new SimpleStepActivityDistribution(
                    group.Key,
                    confidence.Length,
                    confidence.Min(),
                    confidence.Max(),
                    (int)Math.Round(confidence.Average(), MidpointRounding.AwayFromZero));
            })
            .ToArray();
        var intervalId = Hash(string.Join(':',
            input.SessionId.ToString("D"),
            interval.BootSessionId.ToString("D"),
            interval.StartClientSampleId.ToString("D"),
            interval.EndClientSampleId.ToString("D"),
            interval.IntervalStartElapsedNs.ToString(CultureInfo.InvariantCulture),
            interval.IntervalEndElapsedNs.ToString(CultureInfo.InvariantCulture),
            interval.CounterStart.ToString(CultureInfo.InvariantCulture),
            interval.CounterEnd.ToString(CultureInfo.InvariantCulture)));
        var fingerprint = Fingerprint(input, intervalId, windows, regions, evidenceClass);

        return new(
            input.SessionId,
            interval.BootSessionId,
            intervalId,
            interval.IntervalStartElapsedNs,
            interval.IntervalEndElapsedNs,
            interval.CounterDelta,
            Math.Max(0, input.DetectorCount),
            windows.Length,
            motionAccepted,
            motionSuspicious,
            motionRejected,
            motionUnavailable,
            regions.Length,
            ToMilliseconds(fraudDurationNs),
            ToMilliseconds(intervalDurationNs),
            intervalDurationNs <= 0
                ? 0
                : Math.Round((decimal)fraudDurationNs / intervalDurationNs, 6),
            regions.Count(x => x.HardShakeEvidence),
            regions.Length == 0 ? 0 : ToMilliseconds(regions.Max(x => x.DurationNs)),
            Math.Max(0, interval.CounterDelta),
            activities,
            regions,
            evidenceClass,
            fingerprint);
    }

    private static IEnumerable<TemporalMotionEvidenceWindow> Canonicalize(
        TemporalFraudEvaluationInput input) => input.MotionWindows
        .Where(x =>
            x.BootSessionId == input.Interval.BootSessionId &&
            x.WindowEndElapsedNs > x.WindowStartElapsedNs &&
            x.WindowEndElapsedNs > input.Interval.IntervalStartElapsedNs &&
            x.WindowStartElapsedNs < input.Interval.IntervalEndElapsedNs)
        .GroupBy(x => new
        {
            x.BootSessionId,
            x.WindowStartElapsedNs,
            x.WindowEndElapsedNs
        })
        .Select(group => group
            .OrderByDescending(x => x.IsCurrentEvidence)
            .ThenByDescending(x => x.SampleCount)
            .ThenBy(x => x.MotionWindowId)
            .First())
        .OrderBy(x => x.WindowStartElapsedNs)
        .ThenBy(x => x.WindowEndElapsedNs)
        .ThenBy(x => x.MotionWindowId);

    private static IEnumerable<TemporalFraudRegion> BuildRegions(
        IReadOnlyList<(TemporalMotionEvidenceWindow Window, string FraudType)> atomic)
    {
        if (atomic.Count == 0) yield break;
        var current = NewRegion(atomic[0].Window, atomic[0].FraudType);
        for (var index = 1; index < atomic.Count; index++)
        {
            var next = atomic[index];
            if (next.FraudType == current.FraudType &&
                next.Window.WindowStartElapsedNs <= current.EndElapsedNs)
            {
                current = current with
                {
                    EndElapsedNs = Math.Max(current.EndElapsedNs, next.Window.WindowEndElapsedNs),
                    SupportingReasonCodes = current.SupportingReasonCodes
                        .Concat(NormalizeReasons(next.Window.ReasonCodes))
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                    MotionWindowIds = current.MotionWindowIds
                        .Append(next.Window.MotionWindowId)
                        .Distinct()
                        .Order()
                        .ToArray(),
                    ActivityContext = current.ActivityContext
                        .Append(NormalizeActivity(next.Window.ActivityCode))
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray()
                };
                continue;
            }
            yield return current;
            current = NewRegion(next.Window, next.FraudType);
        }
        yield return current;
    }

    private static TemporalFraudRegion NewRegion(
        TemporalMotionEvidenceWindow window,
        string fraudType) => new(
        window.BootSessionId,
        window.WindowStartElapsedNs,
        window.WindowEndElapsedNs,
        fraudType,
        NormalizeReasons(window.ReasonCodes),
        [window.MotionWindowId],
        fraudType == TemporalFraudTypes.HardShake,
        [NormalizeActivity(window.ActivityCode)]);

    private static string? FraudType(TemporalMotionEvidenceWindow window)
    {
        var reasons = NormalizeReasons(window.ReasonCodes);
        return reasons.Contains(HardShakeReason, StringComparer.Ordinal) ||
               (reasons.Contains(GyroscopeShakeReason, StringComparer.Ordinal) &&
                reasons.Contains(AccelerationShakeReason, StringComparer.Ordinal))
            ? TemporalFraudTypes.HardShake
            : null;
    }

    private static bool HasExactlyOneShakeSignal(IEnumerable<string> reasons)
    {
        var normalized = NormalizeReasons(reasons);
        return normalized.Contains(GyroscopeShakeReason, StringComparer.Ordinal) ^
               normalized.Contains(AccelerationShakeReason, StringComparer.Ordinal);
    }

    private static string[] NormalizeReasons(IEnumerable<string> reasons) => reasons
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static long UnionDurationNs(IReadOnlyList<TemporalFraudRegion> regions)
    {
        if (regions.Count == 0) return 0;
        long total = 0;
        var start = regions[0].StartElapsedNs;
        var end = regions[0].EndElapsedNs;
        foreach (var region in regions.Skip(1).OrderBy(x => x.StartElapsedNs))
        {
            if (region.StartElapsedNs <= end)
            {
                end = Math.Max(end, region.EndElapsedNs);
                continue;
            }
            total = checked(total + Math.Max(0, end - start));
            start = region.StartElapsedNs;
            end = region.EndElapsedNs;
        }
        return checked(total + Math.Max(0, end - start));
    }

    private static string Fingerprint(
        TemporalFraudEvaluationInput input,
        string intervalId,
        IReadOnlyList<TemporalMotionEvidenceWindow> windows,
        IReadOnlyList<TemporalFraudRegion> regions,
        string evidenceClass)
    {
        var canonical = new StringBuilder()
            .Append(intervalId).Append('|')
            .Append(input.DetectorCount).Append('|')
            .Append(evidenceClass);
        foreach (var window in windows)
        {
            canonical.Append("\nW:")
                .Append(window.MotionWindowId.ToString("D")).Append(':')
                .Append(window.WindowStartElapsedNs).Append(':')
                .Append(window.WindowEndElapsedNs).Append(':')
                .Append(NormalizeStatus(window.Classification)).Append(':')
                .AppendJoin(',', NormalizeReasons(window.ReasonCodes)).Append(':')
                .Append(NormalizeActivity(window.ActivityCode)).Append(':')
                .Append(window.ActivityConfidence);
        }
        foreach (var region in regions)
        {
            canonical.Append("\nR:")
                .Append(region.StartElapsedNs).Append(':')
                .Append(region.EndElapsedNs).Append(':')
                .Append(region.FraudType).Append(':')
                .AppendJoin(',', region.MotionWindowIds.Order());
        }
        return Hash(canonical.ToString());
    }

    private static int Count(
        IEnumerable<TemporalMotionEvidenceWindow> windows,
        string status) => windows.Count(x => NormalizeStatus(x.Classification) == status);

    private static string NormalizeStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unavailable" : value.Trim().ToLowerInvariant();

    private static string NormalizeActivity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unavailable" : value.Trim().ToLowerInvariant();

    private static long ToMilliseconds(long nanoseconds) =>
        Math.Max(0, nanoseconds) / 1_000_000L;

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
