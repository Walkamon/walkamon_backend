using System.Security.Cryptography;
using System.Text;
using BLL.Options;
using BLL.Service;
using DAL.DTO;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class MotionValidationV3CoverageTests
{
    private static readonly Guid BootId =
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateTime UtcStart =
        new(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
    private const long ElapsedBaseNs = 10_000_000_000L;

    [Fact]
    public void MonotonicHalfOpenMatchingSupportsCurrentAndPreviousWindows()
    {
        var current = MotionValidationEngine.EvaluateV3(
            [Event(0), Event(1000)],
            [Window(0, 1, utcOffsetSeconds: 60)],
            [],
            Options());
        var previous = MotionValidationEngine.EvaluateV3(
            [Event(250, utcOffsetSeconds: 120)],
            [],
            [Window(0, 1)],
            Options());

        Assert.Equal("current", current.Events[0].Coverage!.MatchSource);
        Assert.Equal("none", current.Events[1].Coverage!.MatchSource);
        Assert.Contains("motion_evidence_missing", current.Events[1].Reasons);
        Assert.Equal("previous", previous.Events[0].Coverage!.MatchSource);
    }

    [Fact]
    public void LateWindowKeepsCandidatePendingThenAllowsExistingSupportBudget()
    {
        var detector = Event(250);
        var recordId = Guid.NewGuid();
        var initial = MotionValidationEngine.EvaluateV3(
            [detector], [], [], Options()).Events[0];
        var initialStatus = StepMotionEvidenceRules.NormalizeStatus(
            initial.Status,
            initial.Reasons);

        var waiting = StepSupportBudgetRules.Allocate(1,
        [
            new StepSupportCandidate(
                recordId,
                detector.ClientEventId,
                detector.SensorElapsedRealtimeNs,
                1,
                "pending",
                initialStatus,
                false)
        ]);

        var pending = Assert.Single(waiting.FinalResolutions);
        Assert.Equal("pending", pending.Status);
        Assert.Equal("pending_motion_evidence", pending.Reason);
        Assert.Equal(0, waiting.ConsumedSupportBudget);

        var reevaluated = MotionValidationEngine.EvaluateV3(
            [detector], [], [Window(0, 0)], Options()).Events[0];
        var reevaluatedStatus = StepMotionEvidenceRules.NormalizeStatus(
            reevaluated.Status,
            reevaluated.Reasons);
        var resolved = StepSupportBudgetRules.Allocate(1,
        [
            new StepSupportCandidate(
                recordId,
                detector.ClientEventId,
                detector.SensorElapsedRealtimeNs,
                1,
                "pending",
                reevaluatedStatus,
                false)
        ]);

        Assert.Equal("previous", reevaluated.Coverage!.MatchSource);
        Assert.Equal("accepted", reevaluatedStatus);
        Assert.Equal([recordId], resolved.CandidatesToAccept);
        Assert.Empty(resolved.FinalResolutions);
    }

    [Fact]
    public void MotionLifecycleUsesExistingMaxEvidenceAgePolicy()
    {
        var recordedAt = UtcStart;

        Assert.False(StepMotionEvidenceRules.IsLifecycleClosed(
            recordedAt,
            recordedAt.AddSeconds(119),
            120));
        Assert.True(StepMotionEvidenceRules.IsLifecycleClosed(
            recordedAt,
            recordedAt.AddSeconds(120),
            120));
    }

    [Fact]
    public void SameUtcOnDifferentBootDoesNotMatch()
    {
        var window = Window(0, 1);
        window.BootSessionId = Guid.NewGuid();

        var result = MotionValidationEngine.EvaluateV3(
            [Event(250)], [window], [], Options());

        Assert.Equal("none", result.Events[0].Coverage!.MatchSource);
        Assert.Null(result.Events[0].Coverage!.NearestWindowStart);
    }

    [Fact]
    public void ShortContextProducesUnavailableGaitWithoutScorePenalty()
    {
        var result = MotionValidationEngine.EvaluateV3(
            [Event(250)], [Window(0, 0)], [], Options());

        Assert.Equal("unavailable", result.Events[0].GaitStatus);
        Assert.Equal("accepted", result.Events[0].Status);
        Assert.Equal(100, result.Events[0].Score);
        Assert.DoesNotContain("gait_agreement_low", result.Events[0].Reasons);
    }

    [Fact]
    public void ThreeSecondContextWithOnlyTwoCandidatesKeepsGaitUnavailable()
    {
        var result = MotionValidationEngine.EvaluateV3(
            [Event(250), Event(2250)],
            [Window(0, 0), Window(1, 0), Window(2, 0)],
            [],
            Options());

        Assert.All(result.Events.Values, evaluation =>
        {
            Assert.Equal("unavailable", evaluation.GaitStatus);
            Assert.DoesNotContain("gait_agreement_low", evaluation.Reasons);
        });
    }

    [Fact]
    public void HardShakeRemainsRejectedWhenGaitIsUnavailable()
    {
        var window = Window(0, 1);
        window.ActivityCode = "still";
        window.ActivityConfidence = 90;
        window.AccelerationPeakMilli = 25000;
        window.JerkRmsMilli = 40000;
        window.GyroscopeRmsMilli = 4000;
        window.GyroscopePeakMilli = 8000;
        window.AngularTravelMilliDegrees = 150000;

        var result = MotionValidationEngine.EvaluateV3(
            [Event(250)], [window], [], Options());

        Assert.Equal("rejected", result.Events[0].Status);
        Assert.Contains("hard_shake_majority", result.Events[0].Reasons);
    }

    [Fact]
    public void ThreeSecondContextUsesExistingGaitThresholdAndDedupesHistory()
    {
        var current = new[] { Window(0, 1), Window(1, 1), Window(2, 1) };
        var accepted = MotionValidationEngine.EvaluateV3(
            [Event(250), Event(1250), Event(2250)],
            current,
            current.Select(Clone).ToArray(),
            Options());
        var low = MotionValidationEngine.EvaluateV3(
            [Event(250), Event(1250), Event(2250)],
            [Window(0, 0), Window(1, 0), Window(2, 0)],
            [],
            Options());

        Assert.All(accepted.Events.Values, evaluation =>
        {
            Assert.Equal("accepted", evaluation.GaitStatus);
            Assert.Equal("current", evaluation.Coverage!.MatchSource);
        });
        Assert.All(low.Events.Values, evaluation =>
        {
            Assert.Equal("low", evaluation.GaitStatus);
            Assert.Contains("gait_agreement_low", evaluation.Reasons);
        });
    }

    [Fact]
    public void GaitContextDoesNotAbsorbDistantContinuousHistory()
    {
        var current = new[] { Window(0, 1), Window(1, 1), Window(2, 1) };
        var previousNoise = new[]
        {
            Window(-3, 10),
            Window(-2, 10),
            Window(-1, 10),
            Window(3, 10),
            Window(4, 10)
        };

        var result = MotionValidationEngine.EvaluateV3(
            [Event(250), Event(1250), Event(2250)],
            current,
            previousNoise,
            Options());

        Assert.All(result.Events.Values, evaluation =>
        {
            Assert.Equal("accepted", evaluation.GaitStatus);
            Assert.DoesNotContain("gait_agreement_low", evaluation.Reasons);
        });
    }

    [Fact]
    public void V3CanonicalHashBindsMotionBootAndElapsedBounds()
    {
        var window = Window(0, 1);
        var canonical = string.Join('\n',
            "V3",
            "11111111-2222-3333-4444-555555555555",
            "1",
            "NONCE",
            "dual",
            "M:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee:10000000000:11000000000:1786320000000:1786320001000:25:linear:1:1:2000:8000:12000:700:2000:20000:1800:7500:1:walking:90");
        var expected = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        var actual = StepSensorCanonicalizer.ComputeV3Hash(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            1,
            "NONCE",
            "dual",
            [],
            [],
            [window]);

        Assert.Equal(expected, actual);
        window.WindowEndElapsedRealtimeNs++;
        Assert.NotEqual(
            actual,
            StepSensorCanonicalizer.ComputeV3Hash(
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                1,
                "NONCE",
                "dual",
                [],
                [],
                [window]));
    }

    private static MotionValidationOptions Options() => new();

    private static StepDetectorEventRequest Event(
        int elapsedMilliseconds,
        int utcOffsetSeconds = 0) => new()
    {
        ClientEventId = Guid.NewGuid(),
        BootSessionId = BootId,
        SensorElapsedRealtimeNs = ElapsedBaseNs + elapsedMilliseconds * 1_000_000L,
        RecordedAt = UtcStart.AddSeconds(utcOffsetSeconds).AddMilliseconds(elapsedMilliseconds)
    };

    private static StepMotionWindowRequest Window(
        int startSecond,
        int gaitCycles,
        int utcOffsetSeconds = 0) => new()
    {
        BootSessionId = BootId,
        WindowStartElapsedRealtimeNs = ElapsedBaseNs + startSecond * 1_000_000_000L,
        WindowEndElapsedRealtimeNs = ElapsedBaseNs + (startSecond + 1L) * 1_000_000_000L,
        WindowStartedAt = UtcStart.AddSeconds(utcOffsetSeconds + startSecond),
        WindowEndedAt = UtcStart.AddSeconds(utcOffsetSeconds + startSecond + 1),
        SampleCount = 25,
        AccelerometerSource = "linear",
        GyroscopeAvailable = true,
        ActivityAvailable = true,
        AccelerationRmsMilli = 2000,
        AccelerationPeakMilli = 8000,
        JerkRmsMilli = 12000,
        GyroscopeRmsMilli = 700,
        GyroscopePeakMilli = 2000,
        AngularTravelMilliDegrees = 20000,
        DominantFrequencyMilliHz = 1800,
        PeriodicityBps = 7500,
        GaitCycleCount = gaitCycles,
        ActivityCode = "walking",
        ActivityConfidence = 90
    };

    private static StepMotionWindowRequest Clone(StepMotionWindowRequest value) => new()
    {
        BootSessionId = value.BootSessionId,
        WindowStartElapsedRealtimeNs = value.WindowStartElapsedRealtimeNs,
        WindowEndElapsedRealtimeNs = value.WindowEndElapsedRealtimeNs,
        WindowStartedAt = value.WindowStartedAt,
        WindowEndedAt = value.WindowEndedAt,
        SampleCount = value.SampleCount,
        AccelerometerSource = value.AccelerometerSource,
        GyroscopeAvailable = value.GyroscopeAvailable,
        ActivityAvailable = value.ActivityAvailable,
        AccelerationRmsMilli = value.AccelerationRmsMilli,
        AccelerationPeakMilli = value.AccelerationPeakMilli,
        JerkRmsMilli = value.JerkRmsMilli,
        GyroscopeRmsMilli = value.GyroscopeRmsMilli,
        GyroscopePeakMilli = value.GyroscopePeakMilli,
        AngularTravelMilliDegrees = value.AngularTravelMilliDegrees,
        DominantFrequencyMilliHz = value.DominantFrequencyMilliHz,
        PeriodicityBps = value.PeriodicityBps,
        GaitCycleCount = value.GaitCycleCount,
        ActivityCode = value.ActivityCode,
        ActivityConfidence = value.ActivityConfidence
    };
}
