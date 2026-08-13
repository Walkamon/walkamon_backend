using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BLL.Service;

public static class SimpleStepDecisionCodes
{
    public const string Supported = "SUPPORTED";
    public const string Suspicious = "SUSPICIOUS";
    public const string Blocked = "BLOCKED";
    public const string InsufficientEvidence = "INSUFFICIENT_EVIDENCE";
}

public sealed record SimpleCounterObservation(
    Guid ClientSampleId,
    Guid BootSessionId,
    long SensorElapsedRealtimeNs,
    long CounterTotal);

public sealed record SimpleCounterInterval(
    Guid StartClientSampleId,
    Guid EndClientSampleId,
    Guid BootSessionId,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterStart,
    long CounterEnd,
    long CounterDelta);

public static class SimpleCounterIntervalFactory
{
    public static SimpleCounterInterval? Create(
        SimpleCounterObservation? previous,
        SimpleCounterObservation current)
    {
        if (previous == null ||
            previous.BootSessionId != current.BootSessionId ||
            current.SensorElapsedRealtimeNs <= previous.SensorElapsedRealtimeNs ||
            current.CounterTotal < previous.CounterTotal)
            return null;

        return new(
            previous.ClientSampleId,
            current.ClientSampleId,
            current.BootSessionId,
            previous.SensorElapsedRealtimeNs,
            current.SensorElapsedRealtimeNs,
            previous.CounterTotal,
            current.CounterTotal,
            current.CounterTotal - previous.CounterTotal);
    }
}

public sealed record SimpleStepActivityDistribution(
    string ActivityCode,
    int WindowCount,
    int MinimumConfidence,
    int MaximumConfidence,
    int AverageConfidence);

public sealed record SimpleStepIntervalInput(
    Guid SessionId,
    SimpleCounterInterval Interval,
    int DetectorCount,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    int HardShakeBatchCount,
    bool HardShakeObserved,
    IReadOnlyList<SimpleStepActivityDistribution> ActivityDistribution,
    IReadOnlyList<string> ExistingReasonCodes,
    bool SecurityValid = true,
    bool StructureValid = true);

public sealed record SimpleStepIntervalAssessment(
    Guid SessionId,
    Guid BootSessionId,
    Guid StartClientSampleId,
    Guid EndClientSampleId,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterStart,
    long CounterEnd,
    long CounterDelta,
    int DetectorCount,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    int HardShakeBatchCount,
    bool HardShakeObserved,
    IReadOnlyList<SimpleStepActivityDistribution> ActivityDistribution,
    string SimpleDecision,
    long ShadowSimpleSteps,
    IReadOnlyList<string> ReasonCodes,
    string SimpleIntervalId,
    string EvidenceFingerprint);

public static class SimpleStepIntervalEvaluator
{
    private static readonly HashSet<string> ActivityConflictReasons =
        ["activity_still", "activity_vehicle", "activity_bicycle"];

    public static SimpleStepIntervalAssessment Evaluate(SimpleStepIntervalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var interval = input.Interval;
        var reasons = input.ExistingReasonCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var structuralValid = input.StructureValid &&
            interval.BootSessionId != Guid.Empty &&
            interval.StartClientSampleId != Guid.Empty &&
            interval.EndClientSampleId != Guid.Empty &&
            interval.IntervalEndElapsedNs > interval.IntervalStartElapsedNs &&
            interval.CounterEnd >= interval.CounterStart &&
            interval.CounterDelta == interval.CounterEnd - interval.CounterStart &&
            interval.CounterDelta >= 0;

        string decision;
        if (!input.SecurityValid)
        {
            decision = SimpleStepDecisionCodes.Blocked;
            reasons.Add("security_validation_failed");
        }
        else if (!structuralValid)
        {
            decision = SimpleStepDecisionCodes.Blocked;
            reasons.Add("counter_interval_invalid");
        }
        else if (input.HardShakeObserved)
        {
            decision = SimpleStepDecisionCodes.Blocked;
            reasons.Add("hard_shake_observed");
        }
        else
        {
            var usableMotion = Math.Max(0, input.MotionAccepted) +
                Math.Max(0, input.MotionSuspicious) +
                Math.Max(0, input.MotionRejected);
            var activityConflict = reasons.Any(ActivityConflictReasons.Contains);
            if (input.MotionWindowCount <= 0 || usableMotion == 0)
            {
                decision = SimpleStepDecisionCodes.InsufficientEvidence;
                reasons.Add("motion_evidence_unavailable");
            }
            else if (input.MotionAccepted > 0 &&
                     input.MotionSuspicious == 0 &&
                     input.MotionRejected == 0 &&
                     !activityConflict)
            {
                decision = SimpleStepDecisionCodes.Supported;
                reasons.Add("motion_support_present");
            }
            else
            {
                decision = SimpleStepDecisionCodes.Suspicious;
                reasons.Add(activityConflict
                    ? "activity_conflict"
                    : "motion_mixed_or_conflicting");
            }
        }

        var canonicalReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
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
        var activity = input.ActivityDistribution
            .OrderBy(x => x.ActivityCode, StringComparer.Ordinal)
            .ThenBy(x => x.WindowCount)
            .ToArray();
        var fingerprint = BuildFingerprint(
            input,
            intervalId,
            decision,
            activity,
            canonicalReasons);
        var shadowSteps = decision == SimpleStepDecisionCodes.Supported
            ? interval.CounterDelta
            : 0;

        return new(
            input.SessionId,
            interval.BootSessionId,
            interval.StartClientSampleId,
            interval.EndClientSampleId,
            interval.IntervalStartElapsedNs,
            interval.IntervalEndElapsedNs,
            interval.CounterStart,
            interval.CounterEnd,
            interval.CounterDelta,
            Math.Max(0, input.DetectorCount),
            Math.Max(0, input.MotionWindowCount),
            Math.Max(0, input.MotionAccepted),
            Math.Max(0, input.MotionSuspicious),
            Math.Max(0, input.MotionRejected),
            Math.Max(0, input.MotionUnavailable),
            Math.Max(0, input.HardShakeBatchCount),
            input.HardShakeObserved,
            activity,
            decision,
            shadowSteps,
            canonicalReasons,
            intervalId,
            fingerprint);
    }

    private static string BuildFingerprint(
        SimpleStepIntervalInput input,
        string intervalId,
        string decision,
        IReadOnlyList<SimpleStepActivityDistribution> activity,
        IReadOnlyList<string> reasons)
    {
        var canonical = new StringBuilder()
            .Append(intervalId).Append('|')
            .Append(input.DetectorCount).Append('|')
            .Append(input.MotionWindowCount).Append('|')
            .Append(input.MotionAccepted).Append('|')
            .Append(input.MotionSuspicious).Append('|')
            .Append(input.MotionRejected).Append('|')
            .Append(input.MotionUnavailable).Append('|')
            .Append(input.HardShakeBatchCount).Append('|')
            .Append(input.HardShakeObserved ? '1' : '0').Append('|')
            .Append(input.SecurityValid ? '1' : '0').Append('|')
            .Append(input.StructureValid ? '1' : '0').Append('|')
            .Append(decision).Append('|')
            .AppendJoin(',', reasons);
        foreach (var item in activity)
        {
            canonical.Append("\nA:")
                .Append(item.ActivityCode).Append(':')
                .Append(item.WindowCount).Append(':')
                .Append(item.MinimumConfidence).Append(':')
                .Append(item.MaximumConfidence).Append(':')
                .Append(item.AverageConfidence);
        }
        return Hash(canonical.ToString());
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
