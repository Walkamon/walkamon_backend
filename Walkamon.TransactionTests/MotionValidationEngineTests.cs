using BLL.Options;
using BLL.Service;
using DAL.DTO;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class MotionValidationEngineTests
{
    private static readonly DateTime Start =
        new(2026, 7, 17, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WalkingEvidence_IsAccepted()
    {
        var request = Request(
            [Event(200, 1), Event(700, 1)],
            [Window(gaitCycles: 2)]);

        var result = MotionValidationEngine.Evaluate(request, Options());

        Assert.Equal("accepted", result.Status);
        Assert.All(result.Events.Values, value => Assert.Equal("accepted", value.Status));
    }

    [Fact]
    public void CombinedShakeEvidence_IsRejected()
    {
        var request = Request(
            [Event(200, 1), Event(700, 1)],
            [Window(
                gaitCycles: 2,
                activity: "still",
                activityConfidence: 90,
                accelerationPeak: 25000,
                jerkRms: 40000,
                gyroRms: 4000,
                gyroPeak: 8000,
                orientation: 150000)]);

        var result = MotionValidationEngine.Evaluate(request, Options());

        Assert.Equal("rejected", result.Status);
        Assert.Contains("hard_shake_majority", result.Reasons);
    }

    [Fact]
    public void MissingGyroscope_IsDegradedButAllowed()
    {
        var request = Request(
            [Event(200, 1), Event(700, 1)],
            [Window(gaitCycles: 2, gyroAvailable: false)]);

        var result = MotionValidationEngine.Evaluate(request, Options());

        Assert.Equal("accepted", result.Status);
        Assert.True(result.DegradedEvidence);
        Assert.Equal(90, result.Score);
    }

    [Fact]
    public void MissingCoverage_IsRejected()
    {
        var request = Request(
            [Event(1500, 1)],
            [Window(gaitCycles: 1)]);

        var result = MotionValidationEngine.Evaluate(request, Options());

        Assert.Equal("rejected", result.Events[0].Status);
        Assert.Contains("motion_evidence_missing", result.Events[0].Reasons);
    }

    private static SubmitPvpStepBatchRequest Request(
        List<PvpStepEventRequest> events,
        List<StepMotionWindowRequest> windows) => new()
    {
        ContractVersion = 2,
        Events = events,
        MotionWindows = windows
    };

    private static PvpStepEventRequest Event(int milliseconds, int count) => new()
    {
        IntervalStartedAt = Start.AddMilliseconds(milliseconds),
        RecordedAt = Start.AddMilliseconds(milliseconds),
        StepCount = count
    };

    private static StepMotionWindowRequest Window(
        int gaitCycles,
        bool gyroAvailable = true,
        string activity = "walking",
        int activityConfidence = 90,
        int accelerationPeak = 8000,
        int jerkRms = 12000,
        int gyroRms = 700,
        int gyroPeak = 2000,
        int orientation = 20000) => new()
    {
        WindowStartedAt = Start,
        WindowEndedAt = Start.AddSeconds(1),
        SampleCount = 25,
        AccelerometerSource = "linear",
        GyroscopeAvailable = gyroAvailable,
        ActivityAvailable = true,
        AccelerationRmsMilli = 2000,
        AccelerationPeakMilli = accelerationPeak,
        JerkRmsMilli = jerkRms,
        GyroscopeRmsMilli = gyroAvailable ? gyroRms : null,
        GyroscopePeakMilli = gyroAvailable ? gyroPeak : null,
        OrientationDeltaMilliDegrees = gyroAvailable ? orientation : null,
        DominantFrequencyMilliHz = 1800,
        PeriodicityBps = 7500,
        GaitCycleCount = gaitCycles,
        ActivityCode = activity,
        ActivityConfidence = activityConfidence
    };

    private static MotionValidationOptions Options() => new();
}
