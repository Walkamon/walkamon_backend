using System.Security.Cryptography;
using System.Text;
using BLL.Service;
using DAL.DTO;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class StepSensorCanonicalizerTests
{
    [Fact]
    public void ComputeHash_UsesFixedLineContract_AndUppercaseHex()
    {
        var sessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var events = new List<PvpStepEventRequest>
        {
            new()
            {
                IntervalStartedAt = DateTime.Parse("2026-07-17T03:00:01.000Z").ToUniversalTime(),
                RecordedAt = DateTime.Parse("2026-07-17T03:00:02.000Z").ToUniversalTime(),
                StepCount = 2,
                SensorStartTotal = 1200,
                SensorEndTotal = 1202
            },
            new()
            {
                IntervalStartedAt = DateTime.Parse("2026-07-17T03:00:02.000Z").ToUniversalTime(),
                RecordedAt = DateTime.Parse("2026-07-17T03:00:03.000Z").ToUniversalTime(),
                StepCount = 1
            }
        };
        var canonical = string.Join('\n',
            "11111111-2222-3333-4444-555555555555",
            "1",
            "NONCE",
            "counter",
            "1784257201000:1784257202000:2:1200:1202",
            "1784257202000:1784257203000:1::");
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        var actual = StepSensorCanonicalizer.ComputeHash(sessionId, 1, "NONCE", "counter", events);

        Assert.Equal(expected, actual);
        Assert.Matches("^[0-9A-F]{64}$", actual);
    }

    [Fact]
    public void ComputeHash_PreservesEventOrder()
    {
        var id = Guid.NewGuid();
        var first = Event(1);
        var second = Event(2);

        Assert.NotEqual(
            StepSensorCanonicalizer.ComputeHash(id, 1, "N", "detector", [first, second]),
            StepSensorCanonicalizer.ComputeHash(id, 1, "N", "detector", [second, first]));
    }

    [Fact]
    public void ComputeHash_V2_IncludesMotionEvidence()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var events = new List<PvpStepEventRequest>
        {
            new()
            {
                IntervalStartedAt = DateTime.Parse("2026-07-17T03:00:01.000Z").ToUniversalTime(),
                RecordedAt = DateTime.Parse("2026-07-17T03:00:02.000Z").ToUniversalTime(),
                StepCount = 2,
                SensorStartTotal = 1200,
                SensorEndTotal = 1202
            },
            new()
            {
                IntervalStartedAt = DateTime.Parse("2026-07-17T03:00:02.000Z").ToUniversalTime(),
                RecordedAt = DateTime.Parse("2026-07-17T03:00:03.000Z").ToUniversalTime(),
                StepCount = 1
            }
        };
        var windows = new List<StepMotionWindowRequest>
        {
            new()
            {
                WindowStartedAt = DateTime.Parse("2026-07-17T03:00:01.000Z").ToUniversalTime(),
                WindowEndedAt = DateTime.Parse("2026-07-17T03:00:02.000Z").ToUniversalTime(),
                SampleCount = 25,
                AccelerometerSource = "linear",
                GyroscopeAvailable = true,
                ActivityAvailable = true,
                AccelerationRmsMilli = 2310,
                AccelerationPeakMilli = 8420,
                JerkRmsMilli = 12700,
                GyroscopeRmsMilli = 740,
                GyroscopePeakMilli = 3180,
                OrientationDeltaMilliDegrees = 24500,
                DominantFrequencyMilliHz = 1820,
                PeriodicityBps = 7800,
                GaitCycleCount = 2,
                ActivityCode = "walking",
                ActivityConfidence = 78
            }
        };

        var actual = StepSensorCanonicalizer.ComputeHash(
            id, 1, "NONCE", "counter", 2, events, windows);

        Assert.Equal(
            "3A1F6B8310F18DEA996E098952443ABA6A219CF7BA41C96C498167A4D39DECD2",
            actual);
    }

    private static PvpStepEventRequest Event(int second) => new()
    {
        IntervalStartedAt = new DateTime(2026, 7, 17, 0, 0, second, DateTimeKind.Utc),
        RecordedAt = new DateTime(2026, 7, 17, 0, 0, second, DateTimeKind.Utc),
        StepCount = 1
    };
}
