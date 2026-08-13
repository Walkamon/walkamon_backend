using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class SimpleTemporalSegmentEvaluatorTests
{
    private static readonly Guid SessionId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BootId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly DateTime Epoch =
        new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TouchingFraudWindowsAcrossCounterCallbacksFormOneRegion()
    {
        var segment = Evaluate(
            counters: [Counter(0, 100), Counter(5, 110), Counter(10, 120)],
            motion: [HardShake(4), HardShake(5)],
            nowSeconds: 30);

        Assert.Equal(SimpleTemporalSegmentStatuses.Finalizable, segment.Status);
        Assert.Equal(2, segment.IntervalCount);
        Assert.Equal(1, segment.TemporalEvaluation.FraudRegionCount);
        Assert.Equal(2_000, segment.TemporalEvaluation.MaxFraudRegionDurationMs);
    }

    [Fact]
    public void ContinuousFraudAcrossManyCounterIntervalsBlocksAsOneRegion()
    {
        var windows = Enumerable.Range(1, 19).Select(x => HardShake(x)).ToArray();
        var segment = Evaluate(
            counters: [
                Counter(0, 100), Counter(4, 108), Counter(8, 116),
                Counter(12, 124), Counter(16, 132), Counter(22, 137)
            ],
            motion: windows,
            nowSeconds: 45);

        Assert.Equal(37, segment.AggregateCounterDelta);
        Assert.Equal(1, segment.TemporalEvaluation.FraudRegionCount);
        Assert.Equal(19_000, segment.TemporalEvaluation.MaxFraudRegionDurationMs);
        Assert.Equal(SimpleTemporalPolicyDecisions.Block, segment.FinalDecision?.Decision);
    }

    [Fact]
    public void RealFraudGapRemainsTwoRegionsAndBlocksRepeatedFraud()
    {
        var segment = Evaluate(
            counters: [Counter(0, 100), Counter(10, 120)],
            motion: [HardShake(1), HardShake(7)],
            nowSeconds: 30);

        Assert.Equal(2, segment.TemporalEvaluation.FraudRegionCount);
        Assert.Equal(SimpleTemporalPolicyDecisions.Block, segment.FinalDecision?.Decision);
        Assert.Contains(
            SimpleTemporalPolicyBReasonCodes.RepeatedFraud,
            segment.FinalDecision!.ReasonCodes);
    }

    [Fact]
    public void OneSecondWalkingTransientAllowsWholeCounterAggregate()
    {
        var segment = Evaluate(
            counters: [Counter(0, 100), Counter(10, 151)],
            motion: [Normal(4)],
            nowSeconds: 30);

        Assert.Equal(51, segment.AggregateCounterDelta);
        Assert.Equal(0, segment.TemporalEvaluation.FraudRegionCount);
        Assert.Equal(SimpleTemporalPolicyDecisions.Allow, segment.FinalDecision?.Decision);
        Assert.Equal(51, segment.FinalDecision?.EligibleStepCount);
    }

    [Fact]
    public void IntermediateCounterCallbacksDoNotCreateDecisionsOrLoseDelta()
    {
        var segment = Evaluate(
            counters: [
                Counter(0, 100), Counter(5, 110),
                Counter(10, 125), Counter(15, 137)
            ],
            motion: [Normal(3), Normal(8), Normal(13)],
            nowSeconds: 20);

        Assert.Equal(37, segment.AggregateCounterDelta);
        Assert.Equal(3, segment.IntervalCount);
        Assert.Equal(SimpleTemporalSegmentStatuses.Open, segment.Status);
        Assert.Null(segment.FinalDecision);
    }

    [Fact]
    public void NoEarlyAllowAndLateHardShakeCanBlockBeforeSettlement()
    {
        var counters = new[] { Counter(0, 100), Counter(25, 140) };
        var early = Evaluate(
            counters,
            motion: [Normal(2)],
            nowSeconds: 30);
        var withLateFraud = Evaluate(
            counters,
            motion: [Normal(2), .. Enumerable.Range(4, 17)
                .Select(x => HardShake(x, receivedSecond: 26))],
            nowSeconds: 42);

        Assert.Equal(SimpleTemporalSegmentStatuses.Open, early.Status);
        Assert.Null(early.FinalDecision);
        Assert.Equal(SimpleTemporalSegmentStatuses.Finalizable, withLateFraud.Status);
        Assert.Equal(SimpleTemporalPolicyDecisions.Block, withLateFraud.FinalDecision?.Decision);
    }

    [Fact]
    public void PendingDetectorKeepsSegmentOpenAfterWatermarkDeadline()
    {
        var segment = Evaluate(
            counters: [Counter(0, 100), Counter(10, 120)],
            motion: [Normal(2)],
            nowSeconds: 60,
            detectors: [Detector(4, "pending")]);

        Assert.Equal(1, segment.DetectorPendingCount);
        Assert.Equal(SimpleTemporalSegmentStatuses.Open, segment.Status);
        Assert.Null(segment.FinalDecision);
    }

    [Fact]
    public void SettlementClockChangesLifecycleButNotEvidenceFingerprint()
    {
        var counters = new[] { Counter(0, 100), Counter(10, 120) };
        var motion = new[] { Normal(2) };
        var open = Evaluate(counters, motion, nowSeconds: 20);
        var finalizable = Evaluate(counters, motion, nowSeconds: 30);

        Assert.Equal(SimpleTemporalSegmentStatuses.Open, open.Status);
        Assert.Equal(SimpleTemporalSegmentStatuses.Finalizable, finalizable.Status);
        Assert.Equal(open.SegmentId, finalizable.SegmentId);
        Assert.Equal(open.EvidenceFingerprint, finalizable.EvidenceFingerprint);
    }

    [Theory]
    [InlineData(51)]
    [InlineData(86)]
    public void WalkingReplayAllowsFullCounterAggregate(long counterDelta)
    {
        var segment = Evaluate(
            counters: [Counter(0, 500), Counter(200, 500 + counterDelta)],
            motion: Enumerable.Range(0, 20).Select(x => Normal(x * 5)).ToArray(),
            nowSeconds: 230);

        Assert.Equal(counterDelta, segment.AggregateCounterDelta);
        Assert.Equal(SimpleTemporalPolicyDecisions.Allow, segment.FinalDecision?.Decision);
        Assert.Equal(counterDelta, segment.FinalDecision?.EligibleStepCount);
    }

    [Fact]
    public void ShakeReplayReconstructsTwentySixSecondRegionAndBlocksAllCounterDelta()
    {
        var segment = Evaluate(
            counters: [Counter(0, 700), Counter(198, 752)],
            motion: Enumerable.Range(30, 26).Select(x => HardShake(x)).ToArray(),
            nowSeconds: 230);

        Assert.Equal(52, segment.AggregateCounterDelta);
        Assert.Equal(1, segment.TemporalEvaluation.FraudRegionCount);
        Assert.Equal(26_000, segment.TemporalEvaluation.MaxFraudRegionDurationMs);
        Assert.Equal(SimpleTemporalPolicyDecisions.Block, segment.FinalDecision?.Decision);
        Assert.Equal(0, segment.FinalDecision?.EligibleStepCount);
    }

    [Fact]
    public void DuplicateAndOutOfOrderEvidenceCanonicalizesDeterministically()
    {
        var counters = new[] { Counter(10, 120), Counter(0, 100), Counter(5, 110) };
        var duplicate = Normal(2);
        var first = Evaluate(
            counters,
            motion: [duplicate, duplicate, Normal(7)],
            nowSeconds: 30,
            detectors: [Detector(8, "accepted"), Detector(3, "rejected")]);
        var retry = Evaluate(
            counters.Reverse().ToArray(),
            motion: [Normal(7), duplicate],
            nowSeconds: 30,
            detectors: [Detector(3, "rejected"), Detector(8, "accepted")]);

        Assert.Equal(20, first.AggregateCounterDelta);
        Assert.Equal(2, first.TemporalEvaluation.MotionWindowCount);
        Assert.Equal(first.SegmentId, retry.SegmentId);
        Assert.Equal(first.EvidenceFingerprint, retry.EvidenceFingerprint);
        Assert.Equal(first.FinalDecision?.Decision, retry.FinalDecision?.Decision);
        Assert.Equal(first.FinalDecision?.EligibleStepCount, retry.FinalDecision?.EligibleStepCount);
        Assert.Equal(first.FinalDecision?.ReasonCodes, retry.FinalDecision?.ReasonCodes);
    }

    [Fact]
    public void ModelContainsNoSyntheticStepOrRecoveredTimestampSurface()
    {
        var properties = typeof(SimpleTemporalSegment)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(properties, x =>
            x.Contains("Synthetic", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("RecoveredStep", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("StepTimestamp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RebootAndCounterResetCreateHardPartitionsWithoutCrossBoundaryDelta()
    {
        var bootB = Id(9_999);
        var rows = new[]
        {
            StoredCounter(BootId, 0, 100),
            StoredCounter(BootId, 5, 110),
            StoredCounter(BootId, 10, 2),
            StoredCounter(BootId, 15, 12),
            StoredCounter(bootB, 1, 500),
            StoredCounter(bootB, 6, 507)
        };
        var partitions = rows
            .GroupBy(x => x.BootSessionId)
            .SelectMany(group => ValidatedStepService.PartitionSimpleCounterRun(
                group.OrderBy(x => x.SensorElapsedRealtimeNs).ToArray()))
            .ToArray();

        Assert.Equal(3, partitions.Length);
        Assert.Equal([10L, 10L, 7L], partitions
            .Select(x => x[^1].CounterTotal - x[0].CounterTotal)
            .ToArray());
    }

    [Fact]
    public void FinalizedEndpointIsReusedAsNextSegmentBaseline()
    {
        var rows = new[]
        {
            StoredCounter(BootId, 0, 100),
            StoredCounter(BootId, 5, 110),
            StoredCounter(BootId, 10, 125)
        };
        var finalized = new ValidatedStepRecord
        {
            SensorElapsedRealtimeNs = rows[1].SensorElapsedRealtimeNs,
            SensorEndTotal = rows[1].CounterTotal
        };

        var endpoint = ValidatedStepService.FindSimpleSegmentEndpoint(rows, 0, finalized);

        Assert.Equal(1, endpoint);
        Assert.Equal(15, rows[^1].CounterTotal - rows[endpoint].CounterTotal);
    }

    private static SimpleTemporalSegment Evaluate(
        IReadOnlyList<SimpleTemporalCounterEvidence> counters,
        IReadOnlyList<SimpleTemporalMotionEvidence> motion,
        int nowSeconds,
        IReadOnlyList<SimpleTemporalDetectorEvidence>? detectors = null) =>
        SimpleTemporalSegmentEvaluator.Evaluate(new(
            SessionId,
            counters,
            detectors ?? [],
            motion,
            Epoch.AddSeconds(nowSeconds),
            CounterSettlementSeconds: 15));

    private static SimpleTemporalCounterEvidence Counter(int second, long total) => new(
        Id(1_000 + second),
        BootId,
        second * 1_000_000_000L,
        total,
        Epoch.AddSeconds(second),
        Epoch.AddSeconds(second));

    private static StepCounterEvidenceSample StoredCounter(
        Guid bootId,
        int second,
        long total) => new()
        {
            CounterSampleId = Id(5_000 + second + (bootId == BootId ? 0 : 100)),
            ClientSampleId = Id(6_000 + second + (bootId == BootId ? 0 : 100)),
            BootSessionId = bootId,
            SensorElapsedRealtimeNs = second * 1_000_000_000L,
            CounterTotal = total
        };

    private static SimpleTemporalDetectorEvidence Detector(int second, string status) => new(
        Id(2_000 + second),
        Id(3_000 + second),
        BootId,
        second * 1_000_000_000L,
        1,
        status,
        Epoch.AddSeconds(second));

    private static SimpleTemporalMotionEvidence Normal(int second) => Motion(
        second,
        "accepted",
        [],
        receivedSecond: second + 1);

    private static SimpleTemporalMotionEvidence HardShake(
        int second,
        int? receivedSecond = null) => Motion(
        second,
        "rejected",
        ["gyroscope_shake_pattern", "acceleration_shake_pattern"],
        receivedSecond ?? second + 1);

    private static SimpleTemporalMotionEvidence Motion(
        int second,
        string status,
        IReadOnlyList<string> reasons,
        int receivedSecond) => new(
        new TemporalMotionEvidenceWindow(
            Id(4_000 + second),
            BootId,
            second * 1_000_000_000L,
            (second + 1L) * 1_000_000_000L,
            status,
            reasons,
            status == "rejected" ? "still" : "walking",
            90,
            25),
        Epoch.AddSeconds(receivedSecond),
        BatchSequence: second,
        WindowIndex: second);

    private static Guid Id(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }
}
