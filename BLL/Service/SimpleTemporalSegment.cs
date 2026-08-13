using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BLL.Service;

public static class SimpleTemporalSegmentConstants
{
    public const string RecordSourceCode = "simple_counter_segment";
    public const string OpenReasonCode = "simple_temporal_segment_open";
    public const string LateEvidenceReasonCode = "late_evidence_after_segment_finalization";
}

public static class SimpleTemporalSegmentStatuses
{
    public const string Open = "OPEN";
    public const string Finalizable = "FINALIZABLE";
    public const string Finalized = "FINALIZED";
}

public sealed record SimpleTemporalCounterEvidence(
    Guid ClientSampleId,
    Guid BootSessionId,
    long SensorElapsedRealtimeNs,
    long CounterTotal,
    DateTime ObservedAt,
    DateTime ReceivedAt,
    bool SecurityValid = true,
    bool TimeValid = true);

public sealed record SimpleTemporalDetectorEvidence(
    Guid RecordId,
    Guid? ClientEventId,
    Guid BootSessionId,
    long SensorElapsedRealtimeNs,
    int StepCount,
    string ValidationStatus,
    DateTime ReceivedAt);

public sealed record SimpleTemporalMotionEvidence(
    TemporalMotionEvidenceWindow Window,
    DateTime ReceivedAt,
    int BatchSequence,
    int WindowIndex);

public sealed record SimpleTemporalSegmentInput(
    Guid SessionId,
    IReadOnlyList<SimpleTemporalCounterEvidence> CounterSamples,
    IReadOnlyList<SimpleTemporalDetectorEvidence> DetectorRecords,
    IReadOnlyList<SimpleTemporalMotionEvidence> MotionWindows,
    DateTime Now,
    int CounterSettlementSeconds,
    bool StructureValid = true);

public sealed record SimpleTemporalSegment(
    string SegmentId,
    Guid SessionId,
    Guid BootSessionId,
    Guid StartClientSampleId,
    Guid EndClientSampleId,
    long SegmentStartElapsedNs,
    long SegmentEndElapsedNs,
    long CounterStart,
    long CounterEnd,
    long AggregateCounterDelta,
    int IntervalCount,
    int DetectorCount,
    int DetectorPendingCount,
    DateTime EvidenceWatermark,
    DateTime SettlementDeadline,
    string Status,
    bool SecurityValid,
    bool StructureValid,
    IReadOnlyList<TemporalMotionEvidenceWindow> CanonicalMotionWindows,
    TemporalFraudEvaluation TemporalEvaluation,
    SimpleTemporalPolicyBResult? FinalDecision,
    string EvidenceFingerprint);

/// <summary>
/// Reconstructs and evaluates one maximal, unfinalized Counter run. Counter
/// callbacks inside the run are observations only; only the segment endpoints
/// define the aggregate delta. The evaluator is deterministic and has no
/// persistence or authoritative side effects.
/// </summary>
public static class SimpleTemporalSegmentEvaluator
{
    public static SimpleTemporalSegment Evaluate(SimpleTemporalSegmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CounterSamples.Count < 2)
            throw new ArgumentException("A temporal segment requires at least two Counter samples.", nameof(input));

        var counters = input.CounterSamples
            .GroupBy(x => x.ClientSampleId)
            .Select(group => group
                .OrderBy(x => x.SensorElapsedRealtimeNs)
                .ThenBy(x => x.CounterTotal)
                .First())
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientSampleId)
            .ToArray();
        if (counters.Length < 2)
            throw new ArgumentException("A temporal segment requires two unique Counter samples.", nameof(input));

        var first = counters[0];
        var last = counters[^1];
        var structuralValid = input.StructureValid &&
            input.SessionId != Guid.Empty &&
            first.BootSessionId != Guid.Empty &&
            first.ClientSampleId != Guid.Empty &&
            last.ClientSampleId != Guid.Empty &&
            counters.All(x => x.BootSessionId == first.BootSessionId) &&
            counters.Zip(counters.Skip(1), (left, right) =>
                right.SensorElapsedRealtimeNs > left.SensorElapsedRealtimeNs &&
                right.CounterTotal >= left.CounterTotal).All(x => x) &&
            last.CounterTotal >= first.CounterTotal;
        var aggregateDelta = structuralValid
            ? last.CounterTotal - first.CounterTotal
            : 0;

        var detectors = input.DetectorRecords
            .Where(x =>
                x.BootSessionId == first.BootSessionId &&
                x.SensorElapsedRealtimeNs > first.SensorElapsedRealtimeNs &&
                x.SensorElapsedRealtimeNs <= last.SensorElapsedRealtimeNs)
            .GroupBy(x => x.ClientEventId ?? x.RecordId)
            .Select(group => group
                .OrderByDescending(x => NormalizeStatus(x.ValidationStatus) != "pending")
                .ThenByDescending(x => AsUtc(x.ReceivedAt))
                .ThenBy(x => x.RecordId)
                .First())
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientEventId ?? x.RecordId)
            .ToArray();
        var detectorCount = detectors.Sum(x => Math.Max(0, x.StepCount));
        var pendingDetectorCount = detectors.Count(x =>
            NormalizeStatus(x.ValidationStatus) == "pending");

        var canonicalMotion = input.MotionWindows
            .Where(x =>
                x.Window.BootSessionId == first.BootSessionId &&
                x.Window.WindowEndElapsedNs > x.Window.WindowStartElapsedNs &&
                x.Window.WindowEndElapsedNs > first.SensorElapsedRealtimeNs &&
                x.Window.WindowStartElapsedNs < last.SensorElapsedRealtimeNs)
            .GroupBy(x => new
            {
                x.Window.BootSessionId,
                x.Window.WindowStartElapsedNs,
                x.Window.WindowEndElapsedNs
            })
            .Select(group => group
                .OrderByDescending(x => x.Window.IsCurrentEvidence)
                .ThenByDescending(x => x.Window.SampleCount)
                .ThenBy(x => x.BatchSequence)
                .ThenBy(x => x.WindowIndex)
                .ThenBy(x => x.Window.MotionWindowId)
                .First())
            .OrderBy(x => x.Window.WindowStartElapsedNs)
            .ThenBy(x => x.Window.WindowEndElapsedNs)
            .ThenBy(x => x.Window.MotionWindowId)
            .ToArray();

        var interval = new SimpleCounterInterval(
            first.ClientSampleId,
            last.ClientSampleId,
            first.BootSessionId,
            first.SensorElapsedRealtimeNs,
            last.SensorElapsedRealtimeNs,
            first.CounterTotal,
            last.CounterTotal,
            aggregateDelta);
        var temporal = TemporalFraudRegionEvaluator.Evaluate(new(
            input.SessionId,
            interval,
            detectorCount,
            canonicalMotion.Select(x => x.Window).ToArray()));

        var watermarkCandidates = new List<DateTime> { AsUtc(last.ReceivedAt) };
        watermarkCandidates.AddRange(detectors.Select(x => AsUtc(x.ReceivedAt)));
        watermarkCandidates.AddRange(canonicalMotion.Select(x => AsUtc(x.ReceivedAt)));
        var watermark = watermarkCandidates.Max();
        var settlementDeadline = watermark.AddSeconds(Math.Max(1, input.CounterSettlementSeconds));
        var securityValid = counters.All(x => x.SecurityValid);
        var timeValid = counters.All(x => x.TimeValid);
        structuralValid &= timeValid && aggregateDelta <= int.MaxValue;
        var status = structuralValid &&
                     pendingDetectorCount == 0 &&
                     settlementDeadline <= AsUtc(input.Now)
            ? SimpleTemporalSegmentStatuses.Finalizable
            : SimpleTemporalSegmentStatuses.Open;
        var decision = status == SimpleTemporalSegmentStatuses.Finalizable
            ? SimpleTemporalPolicyB.Evaluate(new(
                aggregateDelta,
                temporal.FraudRegionCount,
                temporal.MaxFraudRegionDurationMs,
                securityValid,
                structuralValid))
            : null;
        var segmentId = Hash(string.Join(':',
            input.SessionId.ToString("D"),
            first.BootSessionId.ToString("D"),
            first.ClientSampleId.ToString("D"),
            last.ClientSampleId.ToString("D"),
            first.SensorElapsedRealtimeNs.ToString(CultureInfo.InvariantCulture),
            last.SensorElapsedRealtimeNs.ToString(CultureInfo.InvariantCulture),
            first.CounterTotal.ToString(CultureInfo.InvariantCulture),
            last.CounterTotal.ToString(CultureInfo.InvariantCulture)));
        var fingerprint = BuildFingerprint(
            segmentId,
            counters,
            detectors,
            canonicalMotion,
            temporal,
            structuralValid,
            securityValid);

        return new(
            segmentId,
            input.SessionId,
            first.BootSessionId,
            first.ClientSampleId,
            last.ClientSampleId,
            first.SensorElapsedRealtimeNs,
            last.SensorElapsedRealtimeNs,
            first.CounterTotal,
            last.CounterTotal,
            aggregateDelta,
            counters.Length - 1,
            detectorCount,
            pendingDetectorCount,
            watermark,
            settlementDeadline,
            status,
            securityValid,
            structuralValid,
            canonicalMotion.Select(x => x.Window).ToArray(),
            temporal,
            decision,
            fingerprint);
    }

    private static string BuildFingerprint(
        string segmentId,
        IReadOnlyList<SimpleTemporalCounterEvidence> counters,
        IReadOnlyList<SimpleTemporalDetectorEvidence> detectors,
        IReadOnlyList<SimpleTemporalMotionEvidence> motion,
        TemporalFraudEvaluation temporal,
        bool structureValid,
        bool securityValid)
    {
        var value = new StringBuilder()
            .Append(segmentId).Append('|')
            .Append(structureValid).Append('|')
            .Append(securityValid).Append('|')
            .Append(temporal.EvidenceFingerprint);
        foreach (var counter in counters)
        {
            value.Append("\nC:")
                .Append(counter.ClientSampleId.ToString("D")).Append(':')
                .Append(counter.SensorElapsedRealtimeNs).Append(':')
                .Append(counter.CounterTotal).Append(':')
                .Append(counter.SecurityValid).Append(':')
                .Append(counter.TimeValid);
        }
        foreach (var detector in detectors)
        {
            value.Append("\nD:")
                .Append((detector.ClientEventId ?? detector.RecordId).ToString("D")).Append(':')
                .Append(detector.SensorElapsedRealtimeNs).Append(':')
                .Append(detector.StepCount).Append(':')
                .Append(NormalizeStatus(detector.ValidationStatus));
        }
        foreach (var window in motion)
        {
            value.Append("\nM:")
                .Append(window.Window.MotionWindowId.ToString("D")).Append(':')
                .Append(window.Window.WindowStartElapsedNs).Append(':')
                .Append(window.Window.WindowEndElapsedNs).Append(':')
                .Append(window.Window.Classification).Append(':')
                .AppendJoin(',', window.Window.ReasonCodes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal));
        }
        return Hash(value.ToString());
    }

    private static string NormalizeStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unavailable" : value.Trim().ToLowerInvariant();

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
