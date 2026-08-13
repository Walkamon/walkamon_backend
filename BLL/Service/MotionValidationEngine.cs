using BLL.Options;
using DAL.DTO;

namespace BLL.Service;

public sealed record MotionEventEvaluation(
    int Score,
    string Status,
    bool DegradedEvidence,
    IReadOnlyList<string> Reasons,
    string GaitStatus = "unavailable",
    MotionCoverageDiagnostic? Coverage = null);

public sealed record MotionCoverageDiagnostic(
    long EventElapsedNs,
    long? NearestWindowStart,
    long? NearestWindowEnd,
    long? GapBeforeNs,
    long? GapAfterNs,
    string MatchSource);

public sealed record V3MotionWindowEvidence(
    StepMotionWindowRequest Window,
    string MatchSource);

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
    private const long MinimumGaitContextNs = 3_000_000_000L;
    private const int MinimumGaitDetectorCandidates = 3;

    private sealed record V3EvaluatedWindow(
        StepMotionWindowRequest Window,
        string MatchSource,
        MotionWindowEvaluation Evaluation);

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
                (window.AngularTravelMilliDegrees ?? window.OrientationDeltaMilliDegrees) >=
                    options.ShakeAngularTravelMilliDegrees;
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

    public static MotionBatchEvaluation EvaluateV3(
        IReadOnlyList<StepDetectorEventRequest> detectorEvents,
        IReadOnlyList<StepMotionWindowRequest> currentWindows,
        IReadOnlyList<StepMotionWindowRequest> previousWindows,
        MotionValidationOptions options)
    {
        if (!options.Enabled)
            return Unavailable(detectorEvents.Count, "motion_validation_disabled");

        var cadenceVariationBps = CalculateV3CadenceVariationBps(detectorEvents);
        var currentEvaluations = currentWindows
            .Select((window, index) => EvaluateV3Window(
                index,
                window,
                detectorEvents.Count,
                cadenceVariationBps,
                options))
            .ToArray();

        var evidence = currentWindows
            .Select((window, index) => new V3EvaluatedWindow(
                window,
                "current",
                currentEvaluations[index]))
            .Concat(previousWindows.Select((window, index) => new V3EvaluatedWindow(
                window,
                "previous",
                EvaluateV3Window(
                    index,
                    window,
                    detectorEvents.Count,
                    cadenceVariationBps,
                    options))))
            .Where(IsUsableV3Identity)
            .GroupBy(x => new
            {
                x.Window.BootSessionId,
                x.Window.WindowStartElapsedRealtimeNs,
                x.Window.WindowEndElapsedRealtimeNs
            })
            .Select(group => group
                .OrderBy(x => SourcePriority(x.MatchSource))
                .ThenByDescending(x => x.Window.SampleCount)
                .First())
            .ToArray();

        var eventResults = new Dictionary<int, MotionEventEvaluation>();
        for (var index = 0; index < detectorEvents.Count; index++)
        {
            var detector = detectorEvents[index];
            var sameBoot = evidence
                .Where(x => x.Window.BootSessionId == detector.BootSessionId)
                .ToArray();
            var matching = sameBoot
                .Where(x => ContainsElapsed(x.Window, detector.SensorElapsedRealtimeNs))
                .OrderBy(x => SourcePriority(x.MatchSource))
                .ThenByDescending(x => x.Window.SampleCount)
                .ThenBy(x => x.Window.WindowStartElapsedRealtimeNs)
                .ToArray();
            var diagnostic = BuildCoverageDiagnostic(detector.SensorElapsedRealtimeNs, sameBoot, matching);

            if (matching.Length == 0)
            {
                eventResults[index] = new(
                    0,
                    "rejected",
                    true,
                    ["motion_evidence_missing", "gait_evidence_unavailable"],
                    "unavailable",
                    diagnostic);
                continue;
            }

            // A detector callback represents a point in sensor time. If multiple
            // windows overlap that point, consume one deterministic feature set
            // instead of counting the same signal more than once.
            var selected = matching[0];
            var score = selected.Evaluation.Score;
            var reasons = selected.Evaluation.Reasons
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var degraded = selected.Evaluation.DegradedEvidence;

            var context = BuildGaitContext(
                detector,
                sameBoot,
                detectorEvents,
                selected);
            var uniqueCoverageNs = CalculateUnionDurationNs(context.Select(x => x.Window));
            var gaitContext = SelectNonOverlappingWindows(context);
            var contextCandidates = detectorEvents
                .Where(candidate =>
                    candidate.BootSessionId == detector.BootSessionId &&
                    context.Any(window => ContainsElapsed(
                        window.Window,
                        candidate.SensorElapsedRealtimeNs)))
                .Select(x => x.ClientEventId)
                .Distinct()
                .Count();

            var gaitStatus = "unavailable";
            if (uniqueCoverageNs < MinimumGaitContextNs ||
                !HasContinuousCoverage(context.Select(x => x.Window)) ||
                contextCandidates < MinimumGaitDetectorCandidates)
            {
                degraded = true;
                reasons.Add("gait_evidence_unavailable");
            }
            else
            {
                var gaitCycles = gaitContext.Sum(x => Math.Max(0, x.Window.GaitCycleCount));
                var agreement = AgreementBps(contextCandidates, gaitCycles);
                if (agreement < options.MinGaitAgreementBps)
                {
                    score -= 35;
                    gaitStatus = "low";
                    reasons.Add("gait_agreement_low");
                }
                else if (agreement < options.PartialGaitAgreementBps)
                {
                    score -= 15;
                    gaitStatus = "partial";
                    reasons.Add("gait_agreement_partial");
                }
                else
                {
                    gaitStatus = "accepted";
                }
            }

            if (detectorEvents.Count >= 2 &&
                CalculateV3CadenceMilliHz(detectorEvents) > options.MaxCadenceMilliHz)
            {
                score = Math.Min(score, options.AcceptedScore - 1);
                reasons.Add("motion_cadence_exceeded");
            }

            if (selected.Evaluation.HardShake)
            {
                score = Math.Min(score, options.RejectedScore - 1);
                reasons.Add("hard_shake_majority");
            }

            score = Math.Clamp(score, 0, 100);
            eventResults[index] = new(
                score,
                Classify(score, options),
                degraded,
                reasons.Distinct(StringComparer.Ordinal).ToArray(),
                gaitStatus,
                diagnostic);
        }

        var batchScore = detectorEvents.Count == 0
            ? 0
            : eventResults.Values.Sum(x => x.Score) / detectorEvents.Count;
        var allReasons = eventResults.Values
            .SelectMany(x => x.Reasons)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new(
            batchScore,
            detectorEvents.Count == 0 ? "unavailable" : Classify(batchScore, options),
            eventResults.Values.Any(x => x.DegradedEvidence),
            allReasons,
            currentEvaluations,
            eventResults);
    }

    private static MotionWindowEvaluation EvaluateV3Window(
        int index,
        StepMotionWindowRequest window,
        int detectorCount,
        int cadenceVariationBps,
        MotionValidationOptions options)
    {
        var reasons = new List<string>();
        var score = 100;
        var degraded = false;
        var durationMs = (window.WindowEndElapsedRealtimeNs -
                          window.WindowStartElapsedRealtimeNs) / 1_000_000d;
        if (durationMs is < 800 or > 1200)
            reasons.Add("motion_window_duration_invalid");
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
            return new(index, 0, "rejected", !window.GyroscopeAvailable, false, reasons);

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
            (window.AngularTravelMilliDegrees ?? window.OrientationDeltaMilliDegrees) >=
                options.ShakeAngularTravelMilliDegrees;
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

        if (detectorCount >= 4 &&
            window.PeriodicityBps >= options.MachinePeriodicityBps &&
            cadenceVariationBps <= options.MachineCadenceVariationBps)
        {
            score -= 15;
            reasons.Add("machine_like_periodicity");
        }

        score = Math.Clamp(score, 0, 100);
        return new(
            index,
            score,
            Classify(score, options),
            degraded,
            gyroShake && accelerationShake,
            reasons);
    }

    private static V3EvaluatedWindow[] SelectNonOverlappingWindows(
        IEnumerable<V3EvaluatedWindow> windows)
    {
        var selected = new List<V3EvaluatedWindow>();
        foreach (var candidate in windows
                     .OrderBy(x => SourcePriority(x.MatchSource))
                     .ThenByDescending(x => x.Window.SampleCount)
                     .ThenBy(x => x.Window.WindowStartElapsedRealtimeNs)
                     .ThenBy(x => x.Window.WindowEndElapsedRealtimeNs))
        {
            if (selected.Any(existing => IntersectsElapsed(existing.Window, candidate.Window)))
                continue;
            selected.Add(candidate);
        }
        return selected
            .OrderBy(x => x.Window.WindowStartElapsedRealtimeNs)
            .ThenBy(x => x.Window.WindowEndElapsedRealtimeNs)
            .ToArray();
    }

    private static IReadOnlyList<V3EvaluatedWindow> BuildGaitContext(
        StepDetectorEventRequest detector,
        IReadOnlyList<V3EvaluatedWindow> availableWindows,
        IReadOnlyList<StepDetectorEventRequest> detectorEvents,
        V3EvaluatedWindow fallback)
    {
        var sameBootEvents = detectorEvents
            .Where(x => x.BootSessionId == detector.BootSessionId)
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientEventId)
            .ToArray();
        if (sameBootEvents.Length < MinimumGaitDetectorCandidates)
            return availableWindows
                .Where(x => ContainsElapsed(x.Window, detector.SensorElapsedRealtimeNs))
                .DefaultIfEmpty(fallback)
                .ToArray();

        var detectorIndex = Array.FindIndex(
            sameBootEvents,
            x => x.ClientEventId == detector.ClientEventId);
        if (detectorIndex < 0) detectorIndex = 0;
        var clusterStart = Math.Clamp(
            detectorIndex - 1,
            0,
            sameBootEvents.Length - MinimumGaitDetectorCandidates);
        var cluster = sameBootEvents
            .Skip(clusterStart)
            .Take(MinimumGaitDetectorCandidates)
            .ToArray();
        var anchors = cluster
            .Select(candidate => availableWindows
                .Where(window => ContainsElapsed(
                    window.Window,
                    candidate.SensorElapsedRealtimeNs))
                .OrderBy(window => SourcePriority(window.MatchSource))
                .ThenByDescending(window => window.Window.SampleCount)
                .FirstOrDefault())
            .Where(x => x != null)
            .Cast<V3EvaluatedWindow>()
            .ToArray();
        if (anchors.Length < MinimumGaitDetectorCandidates)
            return anchors.Append(fallback).Distinct().ToArray();

        var contextStart = anchors.Min(x => x.Window.WindowStartElapsedRealtimeNs);
        var contextEnd = anchors.Max(x => x.Window.WindowEndElapsedRealtimeNs);
        return availableWindows
            .Where(x =>
                x.Window.WindowStartElapsedRealtimeNs < contextEnd &&
                x.Window.WindowEndElapsedRealtimeNs > contextStart)
            .ToArray();
    }

    private static MotionCoverageDiagnostic BuildCoverageDiagnostic(
        long eventElapsedNs,
        IReadOnlyList<V3EvaluatedWindow> sameBoot,
        IReadOnlyList<V3EvaluatedWindow> matching)
    {
        if (matching.Count > 0)
        {
            var match = matching[0];
            return new(
                eventElapsedNs,
                match.Window.WindowStartElapsedRealtimeNs,
                match.Window.WindowEndElapsedRealtimeNs,
                0,
                0,
                match.MatchSource);
        }

        var previous = sameBoot
            .Where(x => x.Window.WindowEndElapsedRealtimeNs <= eventElapsedNs)
            .OrderByDescending(x => x.Window.WindowEndElapsedRealtimeNs)
            .FirstOrDefault();
        var next = sameBoot
            .Where(x => x.Window.WindowStartElapsedRealtimeNs > eventElapsedNs)
            .OrderBy(x => x.Window.WindowStartElapsedRealtimeNs)
            .FirstOrDefault();
        long? gapBefore = previous == null
            ? null
            : eventElapsedNs - previous.Window.WindowEndElapsedRealtimeNs;
        long? gapAfter = next == null
            ? null
            : next.Window.WindowStartElapsedRealtimeNs - eventElapsedNs;
        var nearest = gapBefore.HasValue && (!gapAfter.HasValue || gapBefore <= gapAfter)
            ? previous
            : next;
        return new(
            eventElapsedNs,
            nearest?.Window.WindowStartElapsedRealtimeNs,
            nearest?.Window.WindowEndElapsedRealtimeNs,
            gapBefore,
            gapAfter,
            "none");
    }

    private static long CalculateUnionDurationNs(IEnumerable<StepMotionWindowRequest> windows)
    {
        var intervals = windows
            .Select(x => (
                Start: x.WindowStartElapsedRealtimeNs,
                End: x.WindowEndElapsedRealtimeNs))
            .Where(x => x.End > x.Start)
            .OrderBy(x => x.Start)
            .ToArray();
        if (intervals.Length == 0) return 0;
        var total = 0L;
        var start = intervals[0].Start;
        var end = intervals[0].End;
        for (var index = 1; index < intervals.Length; index++)
        {
            var interval = intervals[index];
            if (interval.Start <= end)
            {
                end = Math.Max(end, interval.End);
                continue;
            }
            total += end - start;
            start = interval.Start;
            end = interval.End;
        }
        return total + end - start;
    }

    private static bool HasContinuousCoverage(
        IEnumerable<StepMotionWindowRequest> windows)
    {
        var intervals = windows
            .Select(x => (
                Start: x.WindowStartElapsedRealtimeNs,
                End: x.WindowEndElapsedRealtimeNs))
            .Where(x => x.End > x.Start)
            .OrderBy(x => x.Start)
            .ToArray();
        if (intervals.Length == 0) return false;
        var end = intervals[0].End;
        for (var index = 1; index < intervals.Length; index++)
        {
            if (intervals[index].Start > end) return false;
            end = Math.Max(end, intervals[index].End);
        }
        return true;
    }

    private static bool IsUsableV3Identity(V3EvaluatedWindow value) =>
        value.Window.BootSessionId != Guid.Empty &&
        value.Window.WindowStartElapsedRealtimeNs > 0 &&
        value.Window.WindowEndElapsedRealtimeNs > value.Window.WindowStartElapsedRealtimeNs;

    private static bool ContainsElapsed(StepMotionWindowRequest window, long eventElapsedNs) =>
        window.WindowStartElapsedRealtimeNs <= eventElapsedNs &&
        eventElapsedNs < window.WindowEndElapsedRealtimeNs;

    private static bool IntersectsElapsed(
        StepMotionWindowRequest left,
        StepMotionWindowRequest right) =>
        left.WindowStartElapsedRealtimeNs < right.WindowEndElapsedRealtimeNs &&
        right.WindowStartElapsedRealtimeNs < left.WindowEndElapsedRealtimeNs;

    private static int SourcePriority(string source) => source == "current" ? 0 : 1;

    private static int CalculateV3CadenceMilliHz(
        IReadOnlyList<StepDetectorEventRequest> events)
    {
        if (events.Count < 2) return 0;
        var ordered = events.Select(x => x.SensorElapsedRealtimeNs).Order().ToArray();
        var durationNs = ordered[^1] - ordered[0];
        if (durationNs <= 0) return int.MaxValue;
        return (int)Math.Clamp(
            Math.Round((events.Count - 1) * 1_000_000_000_000d / durationNs),
            0,
            int.MaxValue);
    }

    private static int CalculateV3CadenceVariationBps(
        IReadOnlyList<StepDetectorEventRequest> events)
    {
        if (events.Count < 4) return 10000;
        var ordered = events.Select(x => x.SensorElapsedRealtimeNs).Order().ToArray();
        var intervals = Enumerable.Range(1, ordered.Length - 1)
            .Select(index => (double)(ordered[index] - ordered[index - 1]))
            .Where(x => x > 0)
            .ToArray();
        if (intervals.Length < 3) return 10000;
        var mean = intervals.Average();
        var variance = intervals.Sum(x => Math.Pow(x - mean, 2)) / intervals.Length;
        return Math.Clamp(
            (int)Math.Round(Math.Sqrt(variance) / mean * 10000d),
            0,
            10000);
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
        (value.AngularTravelMilliDegrees ?? value.OrientationDeltaMilliDegrees) is null or (>= 0 and <= 3600000) &&
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
        var intervals = windows
            .Select(window =>
            {
                var overlapStart = AsUtc(window.WindowStartedAt) > start
                    ? AsUtc(window.WindowStartedAt) : start;
                var overlapEnd = AsUtc(window.WindowEndedAt) < end
                    ? AsUtc(window.WindowEndedAt) : end;
                return (Start: overlapStart, End: overlapEnd);
            })
            .Where(x => x.End > x.Start)
            .OrderBy(x => x.Start)
            .ToArray();
        long coveredTicks = 0;
        if (intervals.Length > 0)
        {
            var mergedStart = intervals[0].Start;
            var mergedEnd = intervals[0].End;
            for (var index = 1; index < intervals.Length; index++)
            {
                var interval = intervals[index];
                if (interval.Start <= mergedEnd)
                {
                    if (interval.End > mergedEnd) mergedEnd = interval.End;
                    continue;
                }
                coveredTicks += (mergedEnd - mergedStart).Ticks;
                mergedStart = interval.Start;
                mergedEnd = interval.End;
            }
            coveredTicks += (mergedEnd - mergedStart).Ticks;
        }
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
