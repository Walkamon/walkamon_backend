using BLL.Options;
using DAL.DTO;

namespace BLL.Service;

public sealed record MotionEventEvaluation(
    int Score,
    string Status,
    bool DegradedEvidence,
    IReadOnlyList<string> Reasons);

public sealed record MotionWindowEvaluation(
    int Index,
    int Score,
    string Status,
    bool DegradedEvidence,
    bool HardShake,
    IReadOnlyList<string> Reasons);

public sealed record MotionBatchEvaluation(
    int Score,
    string Status,
    bool DegradedEvidence,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<MotionWindowEvaluation> Windows,
    IReadOnlyDictionary<int, MotionEventEvaluation> Events);

public static class MotionValidationEngine
{
    private static readonly HashSet<string> ActivityCodes =
        ["walking", "running", "still", "vehicle", "bicycle", "unknown"];
    private static readonly HashSet<string> AccelerometerSources =
        ["linear", "raw_high_pass"];

    public static MotionBatchEvaluation Evaluate(
        SubmitPvpStepBatchRequest request,
        MotionValidationOptions options)
    {
        if (!options.Enabled)
            return Unavailable(request.Events.Count, "motion_validation_disabled");
        if (request.ContractVersion != options.ContractVersion)
            return Unavailable(request.Events.Count, "motion_contract_version_mismatch");
        if (request.MotionWindows.Count == 0)
            return Unavailable(request.Events.Count, "motion_evidence_required");

        var cadenceVariationBps = CalculateCadenceVariationBps(request.Events);
        var windowResults = new List<MotionWindowEvaluation>(request.MotionWindows.Count);
        DateTime? previousEnd = null;

        for (var index = 0; index < request.MotionWindows.Count; index++)
        {
            var window = request.MotionWindows[index];
            var reasons = new List<string>();
            var score = 100;
            var degraded = false;

            var start = AsUtc(window.WindowStartedAt);
            var end = AsUtc(window.WindowEndedAt);
            var durationMs = (end - start).TotalMilliseconds;
            if (durationMs is < 800 or > 1200)
                reasons.Add("motion_window_duration_invalid");
            if (previousEnd.HasValue && start < previousEnd.Value)
                reasons.Add("motion_windows_overlap_or_unsorted");
            previousEnd = end;
            if (window.SampleCount < options.MinSamplesPerWindow ||
                window.SampleCount > options.MaxSamplesPerWindow)
                reasons.Add("motion_sample_count_invalid");
            if (!AccelerometerSources.Contains(window.AccelerometerSource))
                reasons.Add("accelerometer_evidence_unavailable");
            if (!ActivityCodes.Contains(window.ActivityCode))
                reasons.Add("activity_code_invalid");
            if (!ValidRanges(window))
                reasons.Add("motion_feature_out_of_range");

            if (reasons.Count != 0)
            {
                windowResults.Add(new(
                    index, 0, "rejected", !window.GyroscopeAvailable,
                    false, reasons));
                continue;
            }

            if (!window.GyroscopeAvailable)
            {
                score -= 10;
                degraded = true;
                reasons.Add("gyroscope_unavailable");
            }
            if (!window.ActivityAvailable)
            {
                score -= 5;
                degraded = true;
                reasons.Add("activity_recognition_unavailable");
            }
            else if (window.ActivityConfidence >= options.ActivityConfidenceThreshold &&
                     window.ActivityCode is "still" or "vehicle" or "bicycle")
            {
                score -= 30;
                reasons.Add($"activity_{window.ActivityCode}");
            }

            var gyroShake =
                window.GyroscopeAvailable &&
                window.GyroscopeRmsMilli >= options.ShakeGyroscopeRmsMilli &&
                window.GyroscopePeakMilli >= options.ShakeGyroscopePeakMilli &&
                window.OrientationDeltaMilliDegrees >= options.ShakeOrientationDeltaMilliDegrees;
            if (gyroShake)
            {
                score -= 25;
                reasons.Add("gyroscope_shake_pattern");
            }

            var accelerationShake =
                window.AccelerationPeakMilli >= options.ShakeAccelerationPeakMilli &&
                window.JerkRmsMilli >= options.ShakeJerkRmsMilli;
            if (accelerationShake)
            {
                score -= 25;
                reasons.Add("acceleration_shake_pattern");
            }

            if (request.Events.Count >= 4 &&
                window.PeriodicityBps >= options.MachinePeriodicityBps &&
                cadenceVariationBps <= options.MachineCadenceVariationBps)
            {
                score -= 15;
                reasons.Add("machine_like_periodicity");
            }

            score = Math.Clamp(score, 0, 100);
            windowResults.Add(new(
                index,
                score,
                Classify(score, options),
                degraded,
                gyroShake && accelerationShake,
                reasons));
        }

        var eventResults = new Dictionary<int, MotionEventEvaluation>();
        for (var index = 0; index < request.Events.Count; index++)
        {
            var step = request.Events[index];
            var start = AsUtc(step.IntervalStartedAt);
            var end = AsUtc(step.RecordedAt);
            var overlapping = request.MotionWindows
                .Select((window, windowIndex) => (window, windowIndex))
                .Where(x => AsUtc(x.window.WindowEndedAt) > start &&
                            AsUtc(x.window.WindowStartedAt) <= end)
                .ToList();

            if (overlapping.Count == 0)
            {
                eventResults[index] = new(0, "rejected", true, ["motion_evidence_missing"]);
                continue;
            }

            var coverageBps = CalculateCoverageBps(start, end, overlapping.Select(x => x.window));
            if (coverageBps < options.MinCoverageBps)
            {
                eventResults[index] = new(
                    0, "rejected", true, ["motion_evidence_coverage_insufficient"]);
                continue;
            }

            var scores = overlapping.Select(x => windowResults[x.windowIndex].Score).Order().ToArray();
            var score = scores[scores.Length / 2];
            var reasons = overlapping.SelectMany(x => windowResults[x.windowIndex].Reasons)
                .Distinct(StringComparer.Ordinal).ToList();
            var degraded = overlapping.Any(x => windowResults[x.windowIndex].DegradedEvidence);
            var gaitCycles = overlapping.Sum(x => Math.Max(0, x.window.GaitCycleCount));
            var expectedSteps = request.Events
                .Where(candidate => overlapping.Any(x =>
                    AsUtc(x.window.WindowStartedAt) <= AsUtc(candidate.RecordedAt) &&
                    AsUtc(x.window.WindowEndedAt) > AsUtc(candidate.RecordedAt)))
                .Sum(candidate => Math.Max(0, candidate.StepCount));
            if (step.SensorStartTotal.HasValue)
                expectedSteps = Math.Max(0, step.StepCount);
            var agreement = AgreementBps(expectedSteps, gaitCycles);
            if (agreement < options.MinGaitAgreementBps)
            {
                score -= 35;
                reasons.Add("gait_agreement_low");
            }
            else if (agreement < options.PartialGaitAgreementBps)
            {
                score -= 15;
                reasons.Add("gait_agreement_partial");
            }

            if (request.Events.Count >= 2 &&
                CalculateCadenceMilliHz(request.Events) > options.MaxCadenceMilliHz)
            {
                score = Math.Min(score, options.AcceptedScore - 1);
                reasons.Add("motion_cadence_exceeded");
            }

            var hardShakeCount = overlapping.Count(x => windowResults[x.windowIndex].HardShake);
            if (hardShakeCount * 2 >= overlapping.Count)
            {
                score = Math.Min(score, options.RejectedScore - 1);
                reasons.Add("hard_shake_majority");
            }

            score = Math.Clamp(score, 0, 100);
            eventResults[index] = new(
                score, Classify(score, options), degraded,
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        var weightedCount = request.Events.Sum(x => Math.Max(1, x.StepCount));
        var batchScore = weightedCount == 0
            ? 0
            : request.Events.Select((x, i) =>
                    eventResults[i].Score * Math.Max(1, x.StepCount))
                .Sum() / weightedCount;
        var allReasons = eventResults.Values.SelectMany(x => x.Reasons)
            .Distinct(StringComparer.Ordinal).ToArray();
        return new(
            batchScore,
            Classify(batchScore, options),
            eventResults.Values.Any(x => x.DegradedEvidence),
            allReasons,
            windowResults,
            eventResults);
    }

    private static MotionBatchEvaluation Unavailable(int eventCount, string reason)
    {
        var events = Enumerable.Range(0, eventCount).ToDictionary(
            x => x,
            _ => new MotionEventEvaluation(0, "rejected", true, [reason]));
        return new(0, "rejected", true, [reason], [], events);
    }

    private static bool ValidRanges(StepMotionWindowRequest value) =>
        value.SampleCount >= 0 &&
        value.AccelerationRmsMilli is >= 0 and <= 200000 &&
        value.AccelerationPeakMilli is >= 0 and <= 400000 &&
        value.JerkRmsMilli is >= 0 and <= 2000000 &&
        value.GyroscopeRmsMilli is null or (>= 0 and <= 100000) &&
        value.GyroscopePeakMilli is null or (>= 0 and <= 200000) &&
        value.OrientationDeltaMilliDegrees is null or (>= 0 and <= 3600000) &&
        value.DominantFrequencyMilliHz is >= 0 and <= 20000 &&
        value.PeriodicityBps is >= 0 and <= 10000 &&
        value.GaitCycleCount is >= 0 and <= 20 &&
        value.ActivityConfidence is >= 0 and <= 100;

    private static string Classify(int score, MotionValidationOptions options) =>
        score >= options.AcceptedScore
            ? "accepted"
            : score >= options.RejectedScore ? "suspicious" : "rejected";

    private static int AgreementBps(int steps, int gaitCycles)
    {
        if (steps <= 0 || gaitCycles <= 0) return 0;
        return Math.Clamp(
            (int)Math.Round(
                10000m * Math.Min(steps, gaitCycles) / Math.Max(steps, gaitCycles),
                MidpointRounding.AwayFromZero),
            0,
            10000);
    }

    private static int CalculateCoverageBps(
        DateTime start,
        DateTime end,
        IEnumerable<StepMotionWindowRequest> windows)
    {
        if (end <= start)
            return windows.Any(x => AsUtc(x.WindowStartedAt) <= end &&
                                    AsUtc(x.WindowEndedAt) > end) ? 10000 : 0;
        var coveredTicks = windows.Sum(window =>
        {
            var overlapStart = AsUtc(window.WindowStartedAt) > start
                ? AsUtc(window.WindowStartedAt) : start;
            var overlapEnd = AsUtc(window.WindowEndedAt) < end
                ? AsUtc(window.WindowEndedAt) : end;
            return Math.Max(0, (overlapEnd - overlapStart).Ticks);
        });
        return Math.Clamp((int)(coveredTicks * 10000L / (end - start).Ticks), 0, 10000);
    }

    private static int CalculateCadenceMilliHz(IReadOnlyList<PvpStepEventRequest> events)
    {
        if (events.Count < 2) return 0;
        var ordered = events.Select(x => AsUtc(x.RecordedAt)).Order().ToArray();
        var durationSeconds = (ordered[^1] - ordered[0]).TotalSeconds;
        if (durationSeconds <= 0) return int.MaxValue;
        return (int)Math.Round((events.Count - 1) * 1000d / durationSeconds);
    }

    private static int CalculateCadenceVariationBps(IReadOnlyList<PvpStepEventRequest> events)
    {
        if (events.Count < 4) return 10000;
        var ordered = events.Select(x => AsUtc(x.RecordedAt)).Order().ToArray();
        var intervals = Enumerable.Range(1, ordered.Length - 1)
            .Select(i => (ordered[i] - ordered[i - 1]).TotalMilliseconds)
            .Where(x => x > 0)
            .ToArray();
        if (intervals.Length < 3) return 10000;
        var mean = intervals.Average();
        var variance = intervals.Sum(x => Math.Pow(x - mean, 2)) / intervals.Length;
        return Math.Clamp((int)Math.Round(Math.Sqrt(variance) / mean * 10000d), 0, 10000);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
