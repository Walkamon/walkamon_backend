using BLL.Service;
using DAL.DTO;
using DAL.Models;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class StepSensorRulesTests
{
    private static readonly DateTime Now = new(2026, 7, 17, 3, 0, 10, DateTimeKind.Utc);

    [Fact]
    public void Detector_RequiresExactlyOneStep()
    {
        var result = StepSensorRules.ValidateBasic(
            "daily", "detector", Event(Now.AddSeconds(-1), Now, 2), Now, 2, 120);
        Assert.Equal("suspicious", result.Status);
        Assert.Equal("detector_event_must_equal_one_step", result.Reason);
    }

    [Fact]
    public void Detector_RejectsMoreThanFourEventsInOneSecond()
    {
        var events = Enumerable.Range(0, 5)
            .Select(i => Event(Now.AddMilliseconds(i * 100), Now.AddMilliseconds(i * 100), 1))
            .ToList();
        var failures = StepSensorRules.ApplyDetectorCadence(events, []);
        Assert.Equal("detector_1s_cadence_exceeded", failures[4].Reason);
    }

    [Fact]
    public void Detector_RejectsMoreThanEighteenEventsInFiveSeconds()
    {
        var events = Enumerable.Range(0, 19)
            .Select(i => Event(Now.AddMilliseconds(i * 260), Now.AddMilliseconds(i * 260), 1))
            .ToList();
        var failures = StepSensorRules.ApplyDetectorCadence(events, []);
        Assert.Equal("detector_5s_cadence_exceeded", failures[18].Reason);
    }

    [Theory]
    [InlineData(11, 40, 51, "counter_interval_out_of_range")]
    [InlineData(2, 40, 44, "counter_total_mismatch")]
    [InlineData(1, 43, 40, "counter_not_increasing")]
    public void Counter_ValidatesIntervalAndTotals(
        int durationSeconds, long startTotal, long endTotal, string expectedReason)
    {
        var result = StepSensorRules.ValidateBasic("daily", "counter",
            new PvpStepEventRequest
            {
                IntervalStartedAt = Now.AddSeconds(-durationSeconds),
                RecordedAt = Now,
                StepCount = 3,
                SensorStartTotal = startTotal,
                SensorEndTotal = endTotal
            }, Now, 2, 120);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Counter_RejectsResetOrReplay()
    {
        Assert.Equal("counter_reset_or_replay",
            StepSensorRules.ValidateCounterContinuity(1200, 1190).Reason);
    }

    [Theory]
    [InlineData(3, "timestamp_in_future")]
    [InlineData(-121, "timestamp_stale")]
    public void Timestamp_AppliesFutureToleranceAndDailyStaleness(int offsetSeconds, string? expectedReason)
    {
        var result = StepSensorRules.ValidateBasic(
            "daily", "detector", Event(Now.AddSeconds(offsetSeconds), Now.AddSeconds(offsetSeconds), 1),
            Now, 2, 120);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void CounterInterval_UsesLowestMultiplierAcrossEffectBoundary()
    {
        var player = new PvpMatchPlayer { MatchPlayerId = Guid.NewGuid(), PassiveSpeedBps = 0 };
        var match = new PvpMatch { SpeedMinBps = 7500, SpeedMaxBps = 12500 };
        var slow = new PvpMatchEffect
        {
            TargetMatchPlayerId = player.MatchPlayerId,
            EffectKindCode = "debuff",
            MagnitudeBps = 1500,
            StartsAt = Now.AddSeconds(-2),
            EndsAt = Now.AddSeconds(5)
        };

        var multiplier = StepSensorRules.MinimumPvpMultiplier(
            match, player, Now.AddSeconds(-5), Now, [slow]);

        Assert.Equal(8500, multiplier);
    }

    [Theory]
    [InlineData(90, 20, 100, 10)]
    [InlineData(100, 20, 100, 0)]
    [InlineData(10, 20, 100, 20)]
    public void DailyCap_OnlyReturnsRemainingEligibleSteps(
        int current, int requested, int limit, int expected) =>
        Assert.Equal(expected, StepSensorRules.CalculateEligibleUnderDailyCap(current, requested, limit));

    [Fact]
    public void RaceWindow_RequiresTheEntireIntervalInsideRace()
    {
        Assert.True(StepSensorRules.IsIntervalWithinRace(
            Now.AddSeconds(-5), Now, Now.AddSeconds(-10), Now.AddSeconds(10)));
        Assert.False(StepSensorRules.IsIntervalWithinRace(
            Now.AddSeconds(-11), Now, Now.AddSeconds(-10), Now.AddSeconds(10)));
    }

    private static PvpStepEventRequest Event(DateTime start, DateTime end, int steps) => new()
    {
        IntervalStartedAt = start,
        RecordedAt = end,
        StepCount = steps
    };
}
