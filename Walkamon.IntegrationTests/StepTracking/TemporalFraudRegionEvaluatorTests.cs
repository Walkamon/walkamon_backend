using BLL.Options;
using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class TemporalFraudRegionEvaluatorTests
{
    private static readonly Guid SessionId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid BootId =
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void CounterDeltaRemainsAggregateAndCreatesNoSyntheticEvents()
    {
        var interval = SimpleCounterIntervalFactory.Create(
            Observation(0, 100),
            Observation(10, 120));
        var evaluation = Evaluate([], interval!);

        Assert.Equal(20, evaluation.CounterDelta);
        Assert.Equal(20, evaluation.ShadowCounterCandidate);
        Assert.DoesNotContain(
            typeof(TemporalFraudEvaluation).GetProperties(),
            property => property.Name.Contains("AcceptedSteps", StringComparison.Ordinal) ||
                        property.Name.Contains("RecoveredSteps", StringComparison.Ordinal) ||
                        property.Name.Contains("StepTimestamp", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalMotionHasNoClearFraud()
    {
        var evaluation = Evaluate([Normal(0), Normal(1), Normal(2)]);

        Assert.Equal(0, evaluation.FraudRegionCount);
        Assert.Equal(SimpleTemporalEvidenceClasses.NoClearFraud,
            evaluation.SimpleV2EvidenceClass);
    }

    [Fact]
    public void ActivityContextAloneDoesNotCreateFraudVeto()
    {
        var still = Normal(1) with
        {
            ActivityCode = "still",
            ActivityConfidence = 100
        };
        var evaluation = Evaluate([still]);

        Assert.Equal(0, evaluation.FraudRegionCount);
        Assert.Equal(SimpleTemporalEvidenceClasses.NoClearFraud,
            evaluation.SimpleV2EvidenceClass);
        Assert.Equal(20, evaluation.ShadowCounterCandidate);
    }

    [Fact]
    public void MissingMotionIsDescriptiveAndDoesNotZeroCounterCandidate()
    {
        var evaluation = Evaluate([]);

        Assert.Equal(SimpleTemporalEvidenceClasses.InsufficientEvidence,
            evaluation.SimpleV2EvidenceClass);
        Assert.Equal(20, evaluation.ShadowCounterCandidate);
    }

    [Fact]
    public void IsolatedHardShakeCreatesRegionWithoutZeroingCounterCandidate()
    {
        var evaluation = Evaluate([Normal(0), HardShake(1), Normal(2)]);

        Assert.Equal(1, evaluation.FraudRegionCount);
        Assert.Equal(1_000, evaluation.FraudDurationMs);
        Assert.Equal(20, evaluation.ShadowCounterCandidate);
        Assert.Equal(SimpleTemporalEvidenceClasses.FraudRegionPresent,
            evaluation.SimpleV2EvidenceClass);
    }

    [Fact]
    public void ConsecutiveHardShakeWindowsUseExactBoundariesForOneRegion()
    {
        var evaluation = Evaluate([HardShake(2), HardShake(3), HardShake(4)]);
        var region = Assert.Single(evaluation.FraudRegions);

        Assert.Equal(2_000_000_000L, region.StartElapsedNs);
        Assert.Equal(5_000_000_000L, region.EndElapsedNs);
        Assert.Equal(3_000, evaluation.MaxFraudRegionDurationMs);
    }

    [Fact]
    public void HardShakeAtIntervalStartKeepsTimelinePosition()
    {
        var evaluation = Evaluate([HardShake(0), Normal(1)]);

        Assert.Equal(0, Assert.Single(evaluation.FraudRegions).StartElapsedNs);
    }

    [Fact]
    public void HardShakeAtIntervalEndKeepsTimelinePosition()
    {
        var evaluation = Evaluate([Normal(8), HardShake(9)]);

        Assert.Equal(10_000_000_000L, Assert.Single(evaluation.FraudRegions).EndElapsedNs);
    }

    [Fact]
    public void SeparatedFraudWindowsAreNotMerged()
    {
        var evaluation = Evaluate([HardShake(1), Normal(2), HardShake(3)]);

        Assert.Equal(2, evaluation.FraudRegionCount);
        Assert.Equal(2_000, evaluation.FraudDurationMs);
    }

    [Fact]
    public void RebootBoundaryDoesNotCreateTemporalCounterInterval()
    {
        var interval = SimpleCounterIntervalFactory.Create(
            Observation(0, 100),
            Observation(1, 10) with { BootSessionId = Guid.NewGuid() });

        Assert.Null(interval);
    }

    [Fact]
    public void LateCurrentEvidenceWinsCanonicalIdentityAndUpdatesFraudRegion()
    {
        var previous = Normal(1) with { IsCurrentEvidence = false, SampleCount = 40 };
        var current = HardShake(1) with
        {
            MotionWindowId = DeterministicGuid(999),
            IsCurrentEvidence = true,
            SampleCount = 20
        };
        var evaluation = Evaluate([previous, current]);

        Assert.Equal(1, evaluation.MotionWindowCount);
        Assert.Equal(1, evaluation.FraudRegionCount);
    }

    [Fact]
    public async Task RetryDoesNotDuplicateTemporalArtifact()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"walkamon-temporal-shadow-{Guid.NewGuid():N}");
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
            var evaluation = Evaluate([HardShake(1)]);

            await sink.RecordSimpleTemporalShadowIntervalAsync(session, evaluation);
            await sink.RecordSimpleTemporalShadowIntervalAsync(session, evaluation);

            var store = new StepTrackingBenchmarkArtifactStore(directory);
            var artifact = Assert.Single(
                store.ReadTyped<StepTrackingBenchmarkSimpleTemporalShadowInterval>(
                    StepTrackingBenchmarkRecordTypes.SimpleTemporalShadowInterval));
            Assert.False(artifact.Authoritative);
            Assert.Equal(0, artifact.AuthoritativeSteps);
            Assert.Equal(0, artifact.RewardDelta);
            Assert.Equal(0, artifact.ExpDelta);
            Assert.Equal(0, artifact.PvpDelta);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TemporalV2RemainsShadowWhenV3AuthoritativeFlagIsTrue()
    {
        var options = new StepValidationOptions
        {
            V3AuthoritativeEnabled = true,
            SimpleStepValidationEnabled = true,
            SimpleStepValidationRevision = "temporal_v2",
            SimpleStepValidationShadowOnly = true
        };
        var evaluation = Evaluate([HardShake(1)]);
        var properties = typeof(TemporalFraudEvaluation)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.True(options.V3AuthoritativeEnabled);
        Assert.True(options.SimpleStepValidationShadowOnly);
        Assert.Equal(20, evaluation.ShadowCounterCandidate);
        Assert.DoesNotContain("AuthoritativeSteps", properties);
        Assert.DoesNotContain("EligibleStepCount", properties);
        Assert.DoesNotContain("AcceptedSteps", properties);
    }

    private static TemporalFraudEvaluation Evaluate(
        IReadOnlyList<TemporalMotionEvidenceWindow> windows,
        SimpleCounterInterval? interval = null) => TemporalFraudRegionEvaluator.Evaluate(new(
        SessionId,
        interval ?? SimpleCounterIntervalFactory.Create(
            Observation(0, 100),
            Observation(10, 120))!,
        DetectorCount: 8,
        MotionWindows: windows));

    private static TemporalMotionEvidenceWindow Normal(int startSecond) => new(
        DeterministicGuid(startSecond + 100),
        BootId,
        startSecond * 1_000_000_000L,
        (startSecond + 1L) * 1_000_000_000L,
        "accepted",
        [],
        "walking",
        80,
        25);

    private static TemporalMotionEvidenceWindow HardShake(int startSecond) => new(
        DeterministicGuid(startSecond + 200),
        BootId,
        startSecond * 1_000_000_000L,
        (startSecond + 1L) * 1_000_000_000L,
        "rejected",
        ["gyroscope_shake_pattern", "acceleration_shake_pattern"],
        "still",
        90,
        25);

    private static SimpleCounterObservation Observation(int second, long total) => new(
        DeterministicGuid(second + 1),
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
