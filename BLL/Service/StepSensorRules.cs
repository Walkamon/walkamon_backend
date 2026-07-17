using DAL.DTO;
using DAL.Models;

namespace BLL.Service;

public sealed record StepRuleResult(string Status, string? Reason)
{
    public bool IsEligible => Status == "accepted";
}

public static class StepSensorRules
{
    public static int CalculateEligibleUnderDailyCap(int currentEligible, int requested, int limit)
    {
        if (requested <= 0 || limit <= 0 || currentEligible >= limit) return 0;
        return Math.Min(requested, limit - Math.Max(0, currentEligible));
    }

    public static bool IsIntervalWithinRace(
        DateTime intervalStartedAt,
        DateTime recordedAt,
        DateTime? raceStartedAt,
        DateTime? raceEndedAt) =>
        raceStartedAt.HasValue &&
        raceEndedAt.HasValue &&
        AsUtc(intervalStartedAt) >= AsUtc(raceStartedAt.Value) &&
        AsUtc(recordedAt) <= AsUtc(raceEndedAt.Value);

    public static StepRuleResult ValidateBasic(
        string purposeCode,
        string sensorModeCode,
        PvpStepEventRequest item,
        DateTime serverTime,
        int futureToleranceSeconds,
        int dailyMaxAgeSeconds)
    {
        var start = AsUtc(item.IntervalStartedAt);
        var end = AsUtc(item.RecordedAt);
        if (end < start) return Suspicious("interval_reversed");
        if (end > serverTime.AddSeconds(futureToleranceSeconds)) return Suspicious("timestamp_in_future");
        if (purposeCode == "daily" && end < serverTime.AddSeconds(-dailyMaxAgeSeconds)) return Rejected("timestamp_stale");
        if (item.StepCount <= 0) return Rejected("step_count_not_positive");

        if (sensorModeCode == "detector")
        {
            if (item.StepCount != 1) return Suspicious("detector_event_must_equal_one_step");
            if (item.SensorStartTotal.HasValue || item.SensorEndTotal.HasValue)
                return Suspicious("detector_event_must_not_send_sensor_total");
            return Accepted();
        }

        if (sensorModeCode != "counter") return Rejected("sensor_mode_not_supported");
        var duration = (end - start).TotalSeconds;
        if (duration <= 0 || duration > 10) return Suspicious("counter_interval_out_of_range");
        if (!item.SensorStartTotal.HasValue || !item.SensorEndTotal.HasValue)
            return Suspicious("counter_total_required");
        if (item.SensorEndTotal <= item.SensorStartTotal) return Suspicious("counter_not_increasing");
        if (item.SensorEndTotal - item.SensorStartTotal != item.StepCount)
            return Suspicious("counter_total_mismatch");
        if (item.StepCount > Math.Ceiling(duration * 4))
            return Suspicious("counter_cadence_exceeded");
        return Accepted();
    }

    public static IReadOnlyDictionary<int, StepRuleResult> ApplyDetectorCadence(
        IReadOnlyList<PvpStepEventRequest> events,
        IReadOnlyList<DateTime> recentAcceptedTimestamps)
    {
        var result = new Dictionary<int, StepRuleResult>();
        var points = recentAcceptedTimestamps.Select(AsUtc).ToList();
        for (var index = 0; index < events.Count; index++)
        {
            var timestamp = AsUtc(events[index].RecordedAt);
            points.Add(timestamp);
            var oneSecond = points.Count(x => x > timestamp.AddSeconds(-1) && x <= timestamp);
            var fiveSeconds = points.Count(x => x > timestamp.AddSeconds(-5) && x <= timestamp);
            if (oneSecond > 4) result[index] = Suspicious("detector_1s_cadence_exceeded");
            else if (fiveSeconds > 18) result[index] = Suspicious("detector_5s_cadence_exceeded");
        }
        return result;
    }

    public static StepRuleResult ValidateCounterContinuity(long? lastSensorTotal, long? sensorStartTotal)
    {
        if (!lastSensorTotal.HasValue) return Accepted();
        if (!sensorStartTotal.HasValue || sensorStartTotal < lastSensorTotal.Value)
            return Suspicious("counter_reset_or_replay");
        return Accepted();
    }

    public static int MinimumPvpMultiplier(
        PvpMatch match,
        PvpMatchPlayer player,
        DateTime intervalStart,
        DateTime recordedAt,
        IReadOnlyList<PvpMatchEffect> effects)
    {
        var start = AsUtc(intervalStart);
        var end = AsUtc(recordedAt);
        if (end <= start)
            return MultiplierAt(start);

        var boundaries = new List<DateTime> { start, end };
        foreach (var effect in effects)
        {
            if (effect.StartsAt > start && effect.StartsAt < end) boundaries.Add(effect.StartsAt);
            var effectEnd = effect.ConsumedAt ?? effect.EndsAt;
            if (effectEnd > start && effectEnd < end) boundaries.Add(effectEnd);
        }
        boundaries = boundaries.Distinct().Order().ToList();
        var minimum = int.MaxValue;
        for (var i = 0; i < boundaries.Count - 1; i++)
        {
            var point = boundaries[i].AddTicks((boundaries[i + 1] - boundaries[i]).Ticks / 2);
            minimum = Math.Min(minimum, MultiplierAt(point));
        }
        return minimum == int.MaxValue ? MultiplierAt(start) : minimum;

        int MultiplierAt(DateTime point)
        {
            var active = effects
                .Where(x => x.StartsAt <= point && (x.ConsumedAt ?? x.EndsAt) > point && x.EffectKindCode is "buff" or "debuff")
                .Select(x => (x.EffectKindCode, x.MagnitudeBps));
            return PvpGameplayCalculator.CalculateSpeedBps(
                player.PassiveSpeedBps, active, match.SpeedMinBps, match.SpeedMaxBps);
        }
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static StepRuleResult Accepted() => new("accepted", null);
    private static StepRuleResult Rejected(string reason) => new("rejected", reason);
    private static StepRuleResult Suspicious(string reason) => new("suspicious", reason);
}
