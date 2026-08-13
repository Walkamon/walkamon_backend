using BLL.Options;
using BLL.Service;
using DAL.DTO;
using System.Text.Json;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class CounterRecoveryShadowEvaluatorTests
{
    private static readonly Guid SessionId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid BootId =
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateTime UtcStart =
        new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);
    private const long IntervalStartNs = 10_000_000_000L;
    private const long IntervalEndNs = 20_000_000_000L;

    [Fact]
    public void NoCounterExcessProducesNoShadowAssessment()
    {
        var input = Input(detectorCount: 100, counterDelta: 100);

        Assert.Null(CounterRecoveryShadowEvaluator.Evaluate(input));
        Assert.Null(CounterRecoveryShadowEvaluator.Evaluate(
            input with { SettlementClosed = false, CounterDelta = 120 }));
    }

    [Fact]
    public void AcceptedMotionProducesSupportLabelWithoutRecoveredCount()
    {
        var assessment = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 80,
            counterDelta: 100,
            windows: [Window(11, "accepted")]))!;

        Assert.Equal(20, assessment.CounterExcess);
        Assert.Equal(20, assessment.ShadowRecoverableUpperBound);
        Assert.Equal(CounterRecoveryShadowLabels.MotionSupportPresent,
            assessment.ShadowAssessment);
        Assert.DoesNotContain(
            typeof(CounterRecoveryShadowAssessment).GetProperties(),
            property => property.Name.Contains("RecoveredSteps", StringComparison.Ordinal));
    }

    [Fact]
    public void HardShakeBlocksShakeRecoveryHypothesis()
    {
        var assessment = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 72,
            counterDelta: 92,
            windows:
            [
                Window(
                    11,
                    "rejected",
                    reasons: ["gyroscope_shake_pattern", "acceleration_shake_pattern"],
                    batchReasons: ["hard_shake_majority"])
            ]))!;

        Assert.Equal(20, assessment.CounterExcess);
        Assert.True(assessment.HardShakeMajority);
        Assert.Equal(CounterRecoveryShadowLabels.BlockedHardShake,
            assessment.ShadowAssessment);
    }

    [Fact]
    public void MissingMotionProducesInsufficientEvidence()
    {
        var assessment = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 80,
            counterDelta: 100))!;

        Assert.Equal(0, assessment.MotionWindowCount);
        Assert.Equal(CounterRecoveryShadowLabels.InsufficientMotionEvidence,
            assessment.ShadowAssessment);
    }

    [Fact]
    public void ExistingActivityConflictReasonsDriveOnlyDescriptiveLabels()
    {
        var motionConflict = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 80,
            counterDelta: 100,
            windows: [Window(11, "rejected")]))!;
        var conflict = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 80,
            counterDelta: 100,
            windows:
            [
                Window(11, "rejected", "vehicle", ["activity_vehicle"])
            ]))!;
        var mixed = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 80,
            counterDelta: 100,
            windows:
            [
                Window(11, "accepted"),
                Window(12, "rejected", "still", ["activity_still"])
            ]))!;

        Assert.Equal(CounterRecoveryShadowLabels.MotionConflict,
            motionConflict.ShadowAssessment);
        Assert.Equal(CounterRecoveryShadowLabels.ActivityConflict,
            conflict.ShadowAssessment);
        Assert.Equal(CounterRecoveryShadowLabels.MixedEvidence,
            mixed.ShadowAssessment);
    }

    [Fact]
    public void RetryWithSameEvidenceIsDeterministic()
    {
        var input = Input(
            detectorCount: 8,
            counterDelta: 10,
            detectors:
            [
                Detector(11, "accepted"),
                Detector(12, "suspicious")
            ],
            windows:
            [
                Window(11, "accepted"),
                Window(12, "suspicious")
            ]);

        var first = CounterRecoveryShadowEvaluator.Evaluate(input)!;
        var retry = CounterRecoveryShadowEvaluator.Evaluate(input)!;

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(retry));
        Assert.Equal(first.ShadowIntervalId, retry.ShadowIntervalId);
        Assert.Equal(first.EvidenceFingerprint, retry.EvidenceFingerprint);
        Assert.Equal(2, first.GaitDistribution.Single(x =>
            x.GaitStatus == "unavailable").DetectorCount);
        Assert.Equal(2, first.ActivityDistribution.Single(x =>
            x.ActivityCode == "walking").WindowCount);
    }

    [Fact]
    public void CrossBootAndOutsideIntervalEvidenceIsIgnored()
    {
        var otherBoot = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var crossBootWindow = Window(12, "accepted");
        crossBootWindow.Window.BootSessionId = otherBoot;
        var assessment = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 8,
            counterDelta: 10,
            detectors:
            [
                Detector(11, "accepted"),
                Detector(12, "accepted") with { BootSessionId = otherBoot },
                Detector(25, "accepted")
            ],
            windows:
            [
                Window(11, "accepted"),
                crossBootWindow,
                Window(25, "accepted")
            ]))!;

        Assert.Equal(1, assessment.DetectorAcceptedCount);
        Assert.Equal(1, assessment.MotionWindowCount);
    }

    [Fact]
    public void DuplicateWindowIdentityUsesCurrentThenHighestSampleCount()
    {
        var previous = Window(11, "accepted", sampleCount: 40);
        var currentLowSample = Window(11, "suspicious", sampleCount: 20) with
        {
            IsCurrentBatch = true,
            EvidenceId = Guid.Parse("00000000-0000-0000-0000-000000000002")
        };
        var currentHighSample = Window(11, "rejected", sampleCount: 30) with
        {
            IsCurrentBatch = true,
            EvidenceId = Guid.Parse("00000000-0000-0000-0000-000000000003")
        };
        var assessment = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 8,
            counterDelta: 10,
            windows: [previous, currentLowSample, currentHighSample]))!;

        Assert.Equal(1, assessment.MotionWindowCount);
        Assert.Equal(1, assessment.MotionRejectedWindowCount);
        Assert.Equal(0, assessment.MotionAcceptedWindowCount);
    }

    [Fact]
    public void CounterExcessAssessmentContainsNoSyntheticEventIdentityOrTimestamp()
    {
        var assessment = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 0,
            counterDelta: 20,
            windows: [Window(11, "accepted")]))!;
        var propertyNames = typeof(CounterRecoveryShadowAssessment)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.Equal(20, assessment.CounterExcess);
        Assert.DoesNotContain("ClientEventId", propertyNames);
        Assert.DoesNotContain("SensorElapsedRealtimeNs", propertyNames);
        Assert.DoesNotContain("ShadowRecoveredSteps", propertyNames);
    }

    [Fact]
    public void AuthoritativeFlagCannotPromoteCounterRecoveryShadowExcess()
    {
        var options = new StepValidationOptions { V3AuthoritativeEnabled = true };
        var decision = StepReconciliationRules.Evaluate(
            detectorSteps: 8,
            counterDelta: 10,
            settlementClosed: true);
        var detectorCandidates = Enumerable.Range(1, 8)
            .Select(index => new StepSupportCandidate(
                DeterministicGuid(index + 500),
                DeterministicGuid(index + 600),
                (IntervalStartNs + index * 1_000_000L),
                1,
                "pending",
                "accepted",
                false))
            .ToArray();
        var allocation = StepSupportBudgetRules.Allocate(
            decision.SupportBudget,
            detectorCandidates);
        var assessment = CounterRecoveryShadowEvaluator.Evaluate(Input(
            detectorCount: 8,
            counterDelta: 10,
            windows: [Window(11, "accepted")]))!;

        Assert.True(options.V3AuthoritativeEnabled);
        Assert.Equal(8, allocation.CandidatesToAccept.Count);
        Assert.Equal(2, decision.CounterExcessSteps);
        Assert.Equal(2, assessment.CounterExcess);
        Assert.Equal(2, assessment.ShadowRecoverableUpperBound);
        Assert.DoesNotContain(
            typeof(CounterRecoveryShadowAssessment).GetProperties(),
            property => property.Name is "EligibleStepCount" or "AuthoritativeStepCount");
    }

    private static CounterRecoveryShadowInput Input(
        int detectorCount,
        int counterDelta,
        IReadOnlyList<CounterRecoveryShadowDetectorEvidence>? detectors = null,
        IReadOnlyList<CounterRecoveryShadowMotionEvidence>? windows = null) => new(
        SessionId,
        BootId,
        IntervalStartNs,
        IntervalEndNs,
        1_000,
        1_000 + counterDelta,
        counterDelta,
        detectorCount,
        Math.Min(detectorCount, counterDelta),
        true,
        detectors ?? [],
        windows ?? [],
        new MotionValidationOptions());

    private static CounterRecoveryShadowDetectorEvidence Detector(
        int elapsedSecond,
        string validationStatus,
        IReadOnlyList<string>? batchReasons = null) => new(
        DeterministicGuid(elapsedSecond),
        BootId,
        elapsedSecond * 1_000_000_000L,
        UtcStart.AddSeconds(elapsedSecond),
        1,
        validationStatus,
        "accepted",
        batchReasons ?? []);

    private static CounterRecoveryShadowMotionEvidence Window(
        int startSecond,
        string classification,
        string activityCode = "walking",
        IReadOnlyList<string>? reasons = null,
        IReadOnlyList<string>? batchReasons = null,
        int sampleCount = 25) => new(
        DeterministicGuid(startSecond + 100),
        DeterministicGuid(startSecond + 200),
        startSecond,
        false,
        MotionWindow(startSecond, activityCode, sampleCount),
        classification,
        reasons ?? [],
        batchReasons ?? []);

    private static StepMotionWindowRequest MotionWindow(
        int startSecond,
        string activityCode = "walking",
        int sampleCount = 25) => new()
    {
        BootSessionId = BootId,
        WindowStartElapsedRealtimeNs = startSecond * 1_000_000_000L,
        WindowEndElapsedRealtimeNs = (startSecond + 1L) * 1_000_000_000L,
        WindowStartedAt = UtcStart.AddSeconds(startSecond),
        WindowEndedAt = UtcStart.AddSeconds(startSecond + 1),
        SampleCount = sampleCount,
        AccelerometerSource = "linear",
        GyroscopeAvailable = true,
        ActivityAvailable = true,
        AccelerationRmsMilli = 2_000,
        AccelerationPeakMilli = 8_000,
        JerkRmsMilli = 12_000,
        GyroscopeRmsMilli = 700,
        GyroscopePeakMilli = 2_000,
        AngularTravelMilliDegrees = 20_000,
        DominantFrequencyMilliHz = 1_800,
        PeriodicityBps = 7_500,
        GaitCycleCount = 1,
        ActivityCode = activityCode,
        ActivityConfidence = 90
    };

    private static Guid DeterministicGuid(int value)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(value).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
