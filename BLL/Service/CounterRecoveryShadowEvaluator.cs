using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BLL.Options;
using DAL.DTO;

namespace BLL.Service;

public static class CounterRecoveryShadowLabels
{
    public const string BlockedHardShake = "BLOCKED_HARD_SHAKE";
    public const string MotionSupportPresent = "MOTION_SUPPORT_PRESENT";
    public const string MotionConflict = "MOTION_CONFLICT";
    public const string InsufficientMotionEvidence = "INSUFFICIENT_MOTION_EVIDENCE";
    public const string ActivityConflict = "ACTIVITY_CONFLICT";
    public const string MixedEvidence = "MIXED_EVIDENCE";
}

public sealed record CounterRecoveryShadowDetectorEvidence(
    Guid ClientEventId,
    Guid BootSessionId,
    long SensorElapsedRealtimeNs,
    DateTime RecordedAt,
    int StepCount,
    string ValidationStatus,
    string MotionStatus,
    IReadOnlyList<string> BatchMotionReasons);

public sealed record CounterRecoveryShadowMotionEvidence(
    Guid EvidenceId,
    Guid BatchId,
    int BatchSequence,
    bool IsCurrentBatch,
    StepMotionWindowRequest Window,
    string Classification,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> BatchMotionReasons);

public sealed record CounterRecoveryActivityDistribution(
    string ActivityCode,
    int WindowCount,
    int MinimumConfidence,
    int MaximumConfidence,
    int AverageConfidence);

public sealed record CounterRecoveryGaitDistribution(
    string GaitStatus,
    int DetectorCount);

public sealed record CounterRecoveryShadowInput(
    Guid SessionId,
    Guid BootSessionId,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterFrom,
    long CounterTo,
    int CounterDelta,
    int DetectorCount,
    int SupportedDetectorCount,
    bool SettlementClosed,
    IReadOnlyList<CounterRecoveryShadowDetectorEvidence> Detectors,
    IReadOnlyList<CounterRecoveryShadowMotionEvidence> MotionWindows,
    MotionValidationOptions MotionOptions);

public sealed record CounterRecoveryShadowAssessment(
    Guid SessionId,
    Guid BootSessionId,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterFrom,
    long CounterTo,
    int CounterDelta,
    int DetectorCount,
    int SupportedDetectorCount,
    int DetectorAcceptedCount,
    int DetectorSuspiciousCount,
    int DetectorRejectedCount,
    int DetectorPendingCount,
    int CounterExcess,
    int MotionWindowCount,
    int MotionAcceptedWindowCount,
    int MotionSuspiciousWindowCount,
    int MotionRejectedWindowCount,
    int MotionUnavailableWindowCount,
    bool HardShakeMajority,
    IReadOnlyList<CounterRecoveryActivityDistribution> ActivityDistribution,
    IReadOnlyList<CounterRecoveryGaitDistribution> GaitDistribution,
    string ShadowAssessment,
    int ShadowRecoverableUpperBound,
    string ShadowIntervalId,
    string EvidenceFingerprint);

public static class CounterRecoveryShadowEvaluator
{
    private static readonly HashSet<string> ActivityConflictReasons =
        ["activity_still", "activity_vehicle", "activity_bicycle"];
    private static readonly string[] GaitStatuses =
        ["accepted", "partial", "low", "unavailable"];

    public static CounterRecoveryShadowAssessment? Evaluate(
        CounterRecoveryShadowInput input)
    {
        var detectorCount = Math.Max(0, input.DetectorCount);
        var counterDelta = Math.Max(0, input.CounterDelta);
        var counterExcess = Math.Max(0, counterDelta - detectorCount);
        if (!input.SettlementClosed || counterExcess == 0)
            return null;

        var detectors = input.Detectors
            .Where(x =>
                x.BootSessionId == input.BootSessionId &&
                x.SensorElapsedRealtimeNs > input.IntervalStartElapsedNs &&
                x.SensorElapsedRealtimeNs <= input.IntervalEndElapsedNs)
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientEventId)
            .ToArray();
        var windows = CanonicalizeWindows(input).ToArray();

        var detectorAccepted = CountDetectorSteps(detectors, "accepted");
        var detectorSuspicious = CountDetectorSteps(detectors, "suspicious");
        var detectorRejected = CountDetectorSteps(detectors, "rejected");
        var detectorPending = CountDetectorSteps(detectors, "pending");

        var motionAccepted = CountMotionWindows(windows, "accepted");
        var motionSuspicious = CountMotionWindows(windows, "suspicious");
        var motionRejected = CountMotionWindows(windows, "rejected");
        var motionUnavailable = windows.Length -
            motionAccepted - motionSuspicious - motionRejected;

        var hardShakeMajority = detectors.Any(x =>
                ContainsReason(x.BatchMotionReasons, "hard_shake_majority")) ||
            windows.Any(x =>
                ContainsReason(x.ReasonCodes, "hard_shake_majority") ||
                ContainsReason(x.BatchMotionReasons, "hard_shake_majority"));
        var activityConflict = windows.Any(x =>
            x.ReasonCodes.Any(ActivityConflictReasons.Contains) ||
            x.BatchMotionReasons.Any(ActivityConflictReasons.Contains));
        var usableMotion = motionAccepted + motionSuspicious + motionRejected;
        var hasMotionSupport = motionAccepted > 0;
        var hasMotionConflict = motionSuspicious + motionRejected > 0;
        var label = Classify(
            hardShakeMajority,
            usableMotion,
            hasMotionSupport,
            hasMotionConflict,
            activityConflict);

        var activityDistribution = windows
            .GroupBy(x => NormalizeActivityCode(x.Window))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var confidences = group
                    .Select(x => Math.Clamp(x.Window.ActivityConfidence, 0, 100))
                    .ToArray();
                return new CounterRecoveryActivityDistribution(
                    group.Key,
                    confidences.Length,
                    confidences.Min(),
                    confidences.Max(),
                    (int)Math.Round(
                        confidences.Average(),
                        MidpointRounding.AwayFromZero));
            })
            .ToArray();
        var gaitDistribution = EvaluateGaitDistribution(
            detectors,
            windows,
            input.MotionOptions);
        var shadowIntervalId = Hash(string.Join(':',
            input.SessionId.ToString("D"),
            input.BootSessionId.ToString("D"),
            input.IntervalStartElapsedNs.ToString(CultureInfo.InvariantCulture),
            input.IntervalEndElapsedNs.ToString(CultureInfo.InvariantCulture),
            input.CounterFrom.ToString(CultureInfo.InvariantCulture),
            input.CounterTo.ToString(CultureInfo.InvariantCulture)));
        var fingerprint = BuildEvidenceFingerprint(
            input,
            detectors,
            windows,
            hardShakeMajority,
            gaitDistribution,
            label);

        return new(
            input.SessionId,
            input.BootSessionId,
            input.IntervalStartElapsedNs,
            input.IntervalEndElapsedNs,
            input.CounterFrom,
            input.CounterTo,
            counterDelta,
            detectorCount,
            Math.Clamp(input.SupportedDetectorCount, 0, detectorCount),
            detectorAccepted,
            detectorSuspicious,
            detectorRejected,
            detectorPending,
            counterExcess,
            windows.Length,
            motionAccepted,
            motionSuspicious,
            motionRejected,
            motionUnavailable,
            hardShakeMajority,
            activityDistribution,
            gaitDistribution,
            label,
            counterExcess,
            shadowIntervalId,
            fingerprint);
    }

    private static IEnumerable<CounterRecoveryShadowMotionEvidence> CanonicalizeWindows(
        CounterRecoveryShadowInput input) =>
        input.MotionWindows
            .Where(x =>
                x.Window.BootSessionId == input.BootSessionId &&
                x.Window.WindowEndElapsedRealtimeNs > input.IntervalStartElapsedNs &&
                x.Window.WindowStartElapsedRealtimeNs <= input.IntervalEndElapsedNs)
            .GroupBy(x => new
            {
                x.Window.BootSessionId,
                x.Window.WindowStartElapsedRealtimeNs,
                x.Window.WindowEndElapsedRealtimeNs
            })
            .Select(group => group
                .OrderByDescending(x => x.IsCurrentBatch)
                .ThenByDescending(x => x.Window.SampleCount)
                .ThenBy(x => x.BatchSequence)
                .ThenBy(x => x.EvidenceId)
                .First())
            .OrderBy(x => x.Window.WindowStartElapsedRealtimeNs)
            .ThenBy(x => x.Window.WindowEndElapsedRealtimeNs)
            .ThenBy(x => x.EvidenceId);

    private static IReadOnlyList<CounterRecoveryGaitDistribution> EvaluateGaitDistribution(
        IReadOnlyList<CounterRecoveryShadowDetectorEvidence> detectors,
        IReadOnlyList<CounterRecoveryShadowMotionEvidence> windows,
        MotionValidationOptions options)
    {
        var detectorEvents = detectors.Select(x => new StepDetectorEventRequest
        {
            ClientEventId = x.ClientEventId,
            BootSessionId = x.BootSessionId,
            SensorElapsedRealtimeNs = x.SensorElapsedRealtimeNs,
            RecordedAt = x.RecordedAt
        }).ToArray();
        var currentWindows = windows
            .Where(x => x.IsCurrentBatch)
            .Select(x => x.Window)
            .ToArray();
        var previousWindows = windows
            .Where(x => !x.IsCurrentBatch)
            .Select(x => x.Window)
            .ToArray();
        var evaluation = MotionValidationEngine.EvaluateV3(
            detectorEvents,
            currentWindows,
            previousWindows,
            options);
        var normalized = evaluation.Events.Values
            .Select(x => GaitStatuses.Contains(x.GaitStatus, StringComparer.Ordinal)
                ? x.GaitStatus
                : "unavailable")
            .ToArray();
        return GaitStatuses
            .Select(status => new CounterRecoveryGaitDistribution(
                status,
                normalized.Count(x => x == status)))
            .ToArray();
    }

    private static string BuildEvidenceFingerprint(
        CounterRecoveryShadowInput input,
        IReadOnlyList<CounterRecoveryShadowDetectorEvidence> detectors,
        IReadOnlyList<CounterRecoveryShadowMotionEvidence> windows,
        bool hardShakeMajority,
        IReadOnlyList<CounterRecoveryGaitDistribution> gaitDistribution,
        string label)
    {
        var canonical = new StringBuilder()
            .Append(input.SessionId.ToString("D")).Append('|')
            .Append(input.BootSessionId.ToString("D")).Append('|')
            .Append(input.IntervalStartElapsedNs).Append('|')
            .Append(input.IntervalEndElapsedNs).Append('|')
            .Append(input.CounterFrom).Append('|')
            .Append(input.CounterTo).Append('|')
            .Append(input.CounterDelta).Append('|')
            .Append(input.DetectorCount).Append('|')
            .Append(input.SupportedDetectorCount).Append('|')
            .Append(hardShakeMajority ? '1' : '0').Append('|')
            .Append(label);
        foreach (var detector in detectors)
        {
            canonical.Append("\nD:")
                .Append(detector.ClientEventId.ToString("D")).Append(':')
                .Append(detector.SensorElapsedRealtimeNs).Append(':')
                .Append(detector.StepCount).Append(':')
                .Append(NormalizeStatus(detector.ValidationStatus)).Append(':')
                .Append(NormalizeStatus(detector.MotionStatus)).Append(':')
                .AppendJoin(',', detector.BatchMotionReasons.Order(StringComparer.Ordinal));
        }
        foreach (var window in windows)
        {
            canonical.Append("\nM:")
                .Append(window.Window.BootSessionId.ToString("D")).Append(':')
                .Append(window.Window.WindowStartElapsedRealtimeNs).Append(':')
                .Append(window.Window.WindowEndElapsedRealtimeNs).Append(':')
                .Append(window.Window.SampleCount).Append(':')
                .Append(NormalizeStatus(window.Classification)).Append(':')
                .Append(NormalizeActivityCode(window.Window)).Append(':')
                .Append(window.Window.ActivityConfidence).Append(':')
                .AppendJoin(',', window.ReasonCodes.Order(StringComparer.Ordinal))
                .Append(':')
                .AppendJoin(',', window.BatchMotionReasons.Order(StringComparer.Ordinal));
        }
        foreach (var gait in gaitDistribution)
            canonical.Append("\nG:").Append(gait.GaitStatus).Append(':').Append(gait.DetectorCount);
        return Hash(canonical.ToString());
    }

    private static int CountDetectorSteps(
        IEnumerable<CounterRecoveryShadowDetectorEvidence> detectors,
        string status) => detectors
        .Where(x => NormalizeStatus(x.ValidationStatus) == status)
        .Sum(x => Math.Max(0, x.StepCount));

    private static int CountMotionWindows(
        IEnumerable<CounterRecoveryShadowMotionEvidence> windows,
        string status) => windows.Count(x => NormalizeStatus(x.Classification) == status);

    private static bool ContainsReason(IEnumerable<string> reasons, string reason) =>
        reasons.Contains(reason, StringComparer.Ordinal);

    private static string NormalizeActivityCode(StepMotionWindowRequest window) =>
        window.ActivityAvailable && !string.IsNullOrWhiteSpace(window.ActivityCode)
            ? window.ActivityCode.Trim().ToLowerInvariant()
            : "unavailable";

    private static string NormalizeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? "unavailable" : status.Trim().ToLowerInvariant();

    private static string Classify(
        bool hardShakeMajority,
        int usableMotion,
        bool hasMotionSupport,
        bool hasMotionConflict,
        bool activityConflict)
    {
        if (hardShakeMajority)
            return CounterRecoveryShadowLabels.BlockedHardShake;
        if (usableMotion == 0)
            return CounterRecoveryShadowLabels.InsufficientMotionEvidence;
        if (activityConflict && !hasMotionSupport)
            return CounterRecoveryShadowLabels.ActivityConflict;
        if (hasMotionSupport && !hasMotionConflict && !activityConflict)
            return CounterRecoveryShadowLabels.MotionSupportPresent;
        if (hasMotionConflict && !hasMotionSupport && !activityConflict)
            return CounterRecoveryShadowLabels.MotionConflict;
        return CounterRecoveryShadowLabels.MixedEvidence;
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
