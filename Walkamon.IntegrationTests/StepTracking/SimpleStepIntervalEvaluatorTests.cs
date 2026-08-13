using System.Text.Json;
using BLL.Options;
using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class SimpleStepIntervalEvaluatorTests
{
    private static readonly Guid SessionId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid BootId =
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void SameBootCounterObservationsProduceAggregateDeltaOnly()
    {
        var interval = SimpleCounterIntervalFactory.Create(
            Observation(1, 1_000),
            Observation(2, 1_010));

        Assert.NotNull(interval);
        Assert.Equal(10, interval.CounterDelta);
        Assert.DoesNotContain(
            typeof(SimpleCounterInterval).GetProperties(),
            x => x.Name.Contains("DetectorEvent", StringComparison.Ordinal) ||
                 x.Name.Contains("StepTimestamp", StringComparison.Ordinal));
    }

    [Fact]
    public void FirstCounterObservationIsBaselineOnly()
    {
        var interval = SimpleCounterIntervalFactory.Create(
            previous: null,
            Observation(1, 5_000));

        Assert.Null(interval);
    }

    [Fact]
    public void CounterRollbackOrBootChangeDoesNotCreateDelta()
    {
        var rollback = SimpleCounterIntervalFactory.Create(
            Observation(1, 1_000),
            Observation(2, 999));
        var rebooted = SimpleCounterIntervalFactory.Create(
            Observation(1, 1_000),
            Observation(2, 10) with { BootSessionId = Guid.NewGuid() });

        Assert.Null(rollback);
        Assert.Null(rebooted);
    }

    [Fact]
    public void SupportiveMotionUsesCounterCountWithoutDetectorClamp()
    {
        var assessment = Evaluate(counterDelta: 20, detectorCount: 8);

        Assert.Equal(SimpleStepDecisionCodes.Supported, assessment.SimpleDecision);
        Assert.Equal(20, assessment.ShadowSimpleSteps);
        Assert.Equal(8, assessment.DetectorCount);
    }

    [Fact]
    public void DetectorAboveCounterDoesNotIncreaseSimpleCount()
    {
        var assessment = Evaluate(counterDelta: 20, detectorCount: 30);

        Assert.Equal(SimpleStepDecisionCodes.Supported, assessment.SimpleDecision);
        Assert.Equal(20, assessment.ShadowSimpleSteps);
    }

    [Fact]
    public void ExistingHardShakeEvidenceBlocksShadowIntervalOnly()
    {
        var assessment = Evaluate(
            counterDelta: 20,
            detectorCount: 30,
            hardShake: true);

        Assert.Equal(SimpleStepDecisionCodes.Blocked, assessment.SimpleDecision);
        Assert.Equal(0, assessment.ShadowSimpleSteps);
        Assert.Contains("hard_shake_observed", assessment.ReasonCodes);
    }

    [Fact]
    public void ExistingSupportiveMotionProducesSupportedDecision()
    {
        var assessment = Evaluate(counterDelta: 10, detectorCount: 1);

        Assert.Equal(SimpleStepDecisionCodes.Supported, assessment.SimpleDecision);
        Assert.Equal(10, assessment.ShadowSimpleSteps);
    }

    [Fact]
    public void MissingMotionIsNotAutomaticallyAccepted()
    {
        var assessment = Evaluate(
            counterDelta: 10,
            detectorCount: 10,
            motionWindows: 0,
            motionAccepted: 0);

        Assert.Equal(
            SimpleStepDecisionCodes.InsufficientEvidence,
            assessment.SimpleDecision);
        Assert.Equal(0, assessment.ShadowSimpleSteps);
    }

    [Fact]
    public void SameIntervalAndEvidenceAreDeterministicForRetryDedupe()
    {
        var input = Input(counterDelta: 20, detectorCount: 8);
        var first = SimpleStepIntervalEvaluator.Evaluate(input);
        var retry = SimpleStepIntervalEvaluator.Evaluate(input);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(retry));
        Assert.Equal(first.SimpleIntervalId, retry.SimpleIntervalId);
        Assert.Equal(first.EvidenceFingerprint, retry.EvidenceFingerprint);
        Assert.Equal(first.ShadowSimpleSteps, retry.ShadowSimpleSteps);
    }

    [Fact]
    public async Task FileArtifactSinkDoesNotDuplicateSameSimpleIntervalRetry()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"walkamon-simple-shadow-{Guid.NewGuid():N}");
        try
        {
            var sink = new FileStepTrackingBenchmarkSink(new()
            {
                ArtifactDirectory = directory
            });
            var session = new PvpStepSession
            {
                StepSessionId = SessionId,
                CreatedAt = DateTime.UtcNow,
                LastSubmittedAt = DateTime.UtcNow
            };
            var assessment = Evaluate(counterDelta: 20, detectorCount: 8);

            await sink.RecordSimpleShadowIntervalAsync(session, assessment, false);
            await sink.RecordSimpleShadowIntervalAsync(session, assessment, false);

            var store = new StepTrackingBenchmarkArtifactStore(directory);
            Assert.Single(store.ReadTyped<StepTrackingBenchmarkSimpleShadowInterval>(
                StepTrackingBenchmarkRecordTypes.SimpleShadowInterval));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FailedSecurityCannotReachSupportedDecision()
    {
        var assessment = SimpleStepIntervalEvaluator.Evaluate(
            Input(counterDelta: 20, detectorCount: 8) with
            {
                SecurityValid = false
            });

        Assert.Equal(SimpleStepDecisionCodes.Blocked, assessment.SimpleDecision);
        Assert.Equal(0, assessment.ShadowSimpleSteps);
        Assert.Contains("security_validation_failed", assessment.ReasonCodes);
    }

    [Fact]
    public void AuthoritativeV3FlagCannotPromoteSimpleShadowAssessment()
    {
        var options = new StepValidationOptions
        {
            V3AuthoritativeEnabled = true,
            SimpleStepValidationEnabled = true,
            SimpleStepValidationShadowOnly = true
        };
        var assessment = Evaluate(counterDelta: 20, detectorCount: 8);
        var propertyNames = typeof(SimpleStepIntervalAssessment)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.True(options.V3AuthoritativeEnabled);
        Assert.True(options.SimpleStepValidationShadowOnly);
        Assert.Equal(20, assessment.ShadowSimpleSteps);
        Assert.DoesNotContain("EligibleStepCount", propertyNames);
        Assert.DoesNotContain("AuthoritativeSteps", propertyNames);
        Assert.DoesNotContain("NewlyAuthoritative", propertyNames);
    }

    private static SimpleStepIntervalAssessment Evaluate(
        int counterDelta,
        int detectorCount,
        bool hardShake = false,
        int motionWindows = 1,
        int motionAccepted = 1) => SimpleStepIntervalEvaluator.Evaluate(Input(
            counterDelta,
            detectorCount,
            hardShake,
            motionWindows,
            motionAccepted));

    private static SimpleStepIntervalInput Input(
        int counterDelta,
        int detectorCount,
        bool hardShake = false,
        int motionWindows = 1,
        int motionAccepted = 1)
    {
        var interval = SimpleCounterIntervalFactory.Create(
            Observation(1, 1_000),
            Observation(2, 1_000 + counterDelta))!;
        return new(
            SessionId,
            interval,
            detectorCount,
            motionWindows,
            motionAccepted,
            MotionSuspicious: 0,
            MotionRejected: 0,
            MotionUnavailable: Math.Max(0, motionWindows - motionAccepted),
            HardShakeBatchCount: hardShake ? 1 : 0,
            HardShakeObserved: hardShake,
            ActivityDistribution:
            [
                new("walking", Math.Max(0, motionWindows), 0, 0, 0)
            ],
            ExistingReasonCodes: []);
    }

    private static SimpleCounterObservation Observation(int second, long total) => new(
        DeterministicGuid(second),
        BootId,
        second * 1_000_000_000L,
        total);

    private static Guid DeterministicGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }
}
