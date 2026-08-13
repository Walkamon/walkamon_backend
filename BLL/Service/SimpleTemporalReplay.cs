using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BLL.Service;

public sealed record SimpleTemporalReplayEvidence(
    StepTrackingBenchmarkSummaryRow Trial,
    SimpleCounterInterval? CounterInterval,
    IReadOnlyList<TemporalMotionEvidenceWindow> MotionWindows,
    IReadOnlyList<long> DetectorElapsedNs,
    IReadOnlyList<long> CurrentAcceptedDetectorElapsedNs);

public sealed record SimpleTemporalReplayRow(
    string TrialId,
    string Scenario,
    int GroundTruth,
    int CounterDelta,
    int DetectorCount,
    int CurrentV3Accepted,
    int MotionWindowCount,
    int FraudRegionCount,
    long FraudDurationMs,
    long IntervalDurationMs,
    decimal FraudCoverageRatio,
    int HardShakeRegionCount,
    long MaxFraudRegionDurationMs,
    int HardShakeBatchCount,
    string SimpleV1Decision,
    string SimpleV2EvidenceClass,
    int CurrentV3Error,
    int CounterRawError,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    int CurrentAcceptedInsideFraud,
    int CurrentAcceptedBeforeFirstFraud,
    int CurrentAcceptedBetweenFraudRegions,
    int CurrentAcceptedAfterLastFraud,
    int DetectorBeforeFirstFraud,
    int DetectorAfterLastFraud,
    int MotionBeforeFirstFraud,
    int MotionAfterLastFraud,
    string MotionBeforeFraudDistributionJson,
    string MotionAfterFraudDistributionJson,
    long? FirstFraudStartOffsetMs,
    long? LastFraudEndOffsetMs,
    string ActivityDistributionJson,
    string ActivityBeforeFraudJson,
    string ActivityAfterFraudJson,
    string FraudRegionsJson,
    string ReplayScope);

public static class SimpleTemporalReplayBuilder
{
    public static IReadOnlyList<SimpleTemporalReplayRow> Build(
        IReadOnlyList<SimpleTemporalReplayEvidence> evidence) => evidence
        .OrderBy(x => x.Trial.StartedAtUtc)
        .ThenBy(x => x.Trial.TrialId, StringComparer.Ordinal)
        .Select(Build)
        .ToArray();

    private static SimpleTemporalReplayRow Build(SimpleTemporalReplayEvidence source)
    {
        var trial = source.Trial;
        if (source.CounterInterval == null)
        {
            return new(
                trial.TrialId,
                trial.Scenario,
                trial.GroundTruthSteps,
                trial.CounterDelta,
                trial.DetectorPersisted,
                trial.DetectorAccepted,
                trial.MotionWindowCount,
                0, 0, 0, 0, 0, 0,
                trial.HardShakeBatchCount,
                ResolveSimpleV1Decision(trial),
                SimpleTemporalEvidenceClasses.InsufficientEvidence,
                trial.DetectorAccepted - trial.GroundTruthSteps,
                trial.CounterDelta - trial.GroundTruthSteps,
                trial.MotionAccepted,
                trial.MotionSuspicious,
                trial.MotionRejected,
                trial.MotionUnavailable,
                0, 0, 0, 0, 0, 0, 0, 0,
                "[]", "[]",
                null, null,
                trial.ActivityDistributionJson,
                "[]", "[]", "[]",
                "db_temporal_finalized");
        }

        var canonicalWindows = source.MotionWindows
            .Where(x =>
                x.BootSessionId == source.CounterInterval.BootSessionId &&
                x.WindowEndElapsedNs > source.CounterInterval.IntervalStartElapsedNs &&
                x.WindowStartElapsedNs < source.CounterInterval.IntervalEndElapsedNs)
            .GroupBy(x => new
            {
                x.BootSessionId,
                x.WindowStartElapsedNs,
                x.WindowEndElapsedNs
            })
            .Select(group => group
                .OrderByDescending(x => x.IsCurrentEvidence)
                .ThenByDescending(x => x.SampleCount)
                .ThenBy(x => x.MotionWindowId)
                .First())
            .OrderBy(x => x.WindowStartElapsedNs)
            .ThenBy(x => x.WindowEndElapsedNs)
            .ThenBy(x => x.MotionWindowId)
            .ToArray();
        var evaluation = TemporalFraudRegionEvaluator.Evaluate(new(
            trial.SessionId,
            source.CounterInterval,
            trial.DetectorPersisted,
            canonicalWindows));
        var regions = evaluation.FraudRegions
            .OrderBy(x => x.StartElapsedNs)
            .ThenBy(x => x.EndElapsedNs)
            .ToArray();
        var firstStart = regions.FirstOrDefault()?.StartElapsedNs;
        var lastEnd = regions.LastOrDefault()?.EndElapsedNs;
        var accepted = source.CurrentAcceptedDetectorElapsedNs
            .Where(value =>
                value > evaluation.IntervalStartElapsedNs &&
                value <= evaluation.IntervalEndElapsedNs)
            .ToArray();
        var intervalDetectors = source.DetectorElapsedNs
            .Where(value =>
                value > evaluation.IntervalStartElapsedNs &&
                value <= evaluation.IntervalEndElapsedNs)
            .ToArray();
        var inside = accepted.Count(value => regions.Any(region =>
            region.StartElapsedNs <= value && value < region.EndElapsedNs));
        var before = firstStart.HasValue
            ? accepted.Count(value => value < firstStart.Value)
            : 0;
        var after = lastEnd.HasValue
            ? accepted.Count(value => value >= lastEnd.Value)
            : 0;
        var between = regions.Length >= 2
            ? accepted.Count(value =>
                value >= regions[0].EndElapsedNs &&
                value < regions[^1].StartElapsedNs &&
                !regions.Any(region =>
                    region.StartElapsedNs <= value && value < region.EndElapsedNs))
            : 0;
        var beforeWindows = firstStart.HasValue
            ? canonicalWindows.Where(x => x.WindowEndElapsedNs <= firstStart.Value).ToArray()
            : [];
        var afterWindows = lastEnd.HasValue
            ? canonicalWindows.Where(x => x.WindowStartElapsedNs >= lastEnd.Value).ToArray()
            : [];

        return new(
            trial.TrialId,
            trial.Scenario,
            trial.GroundTruthSteps,
            trial.CounterDelta,
            trial.DetectorPersisted,
            trial.DetectorAccepted,
            evaluation.MotionWindowCount,
            evaluation.FraudRegionCount,
            evaluation.FraudDurationMs,
            evaluation.IntervalDurationMs,
            evaluation.FraudCoverageRatio,
            evaluation.HardShakeRegionCount,
            evaluation.MaxFraudRegionDurationMs,
            trial.HardShakeBatchCount,
            ResolveSimpleV1Decision(trial),
            evaluation.SimpleV2EvidenceClass,
            trial.DetectorAccepted - trial.GroundTruthSteps,
            trial.CounterDelta - trial.GroundTruthSteps,
            evaluation.MotionAccepted,
            evaluation.MotionSuspicious,
            evaluation.MotionRejected,
            evaluation.MotionUnavailable,
            inside,
            before,
            between,
            after,
            firstStart.HasValue
                ? intervalDetectors.Count(x => x < firstStart.Value)
                : 0,
            lastEnd.HasValue
                ? intervalDetectors.Count(x => x >= lastEnd.Value)
                : 0,
            beforeWindows.Length,
            afterWindows.Length,
            JsonSerializer.Serialize(
                MotionClassifications(beforeWindows),
                StepTrackingBenchmarkArtifactStore.JsonOptions),
            JsonSerializer.Serialize(
                MotionClassifications(afterWindows),
                StepTrackingBenchmarkArtifactStore.JsonOptions),
            firstStart.HasValue
                ? ToMilliseconds(firstStart.Value - evaluation.IntervalStartElapsedNs)
                : null,
            lastEnd.HasValue
                ? ToMilliseconds(lastEnd.Value - evaluation.IntervalStartElapsedNs)
                : null,
            JsonSerializer.Serialize(
                evaluation.ActivityDistribution,
                StepTrackingBenchmarkArtifactStore.JsonOptions),
            JsonSerializer.Serialize(
                Activity(beforeWindows),
                StepTrackingBenchmarkArtifactStore.JsonOptions),
            JsonSerializer.Serialize(
                Activity(afterWindows),
                StepTrackingBenchmarkArtifactStore.JsonOptions),
            JsonSerializer.Serialize(
                regions,
                StepTrackingBenchmarkArtifactStore.JsonOptions),
            "db_temporal_finalized");
    }

    private static IReadOnlyList<MotionClassificationCount> MotionClassifications(
        IReadOnlyList<TemporalMotionEvidenceWindow> windows) => windows
        .GroupBy(x => string.IsNullOrWhiteSpace(x.Classification)
            ? "unavailable"
            : x.Classification.Trim().ToLowerInvariant())
        .OrderBy(x => x.Key, StringComparer.Ordinal)
        .Select(group => new MotionClassificationCount(group.Key, group.Count()))
        .ToArray();

    private static IReadOnlyList<SimpleStepActivityDistribution> Activity(
        IReadOnlyList<TemporalMotionEvidenceWindow> windows) => windows
        .GroupBy(x => string.IsNullOrWhiteSpace(x.ActivityCode)
            ? "unavailable"
            : x.ActivityCode.Trim().ToLowerInvariant())
        .OrderBy(x => x.Key, StringComparer.Ordinal)
        .Select(group =>
        {
            var confidence = group.Select(x => Math.Clamp(x.ActivityConfidence, 0, 100)).ToArray();
            return new SimpleStepActivityDistribution(
                group.Key,
                confidence.Length,
                confidence.Min(),
                confidence.Max(),
                (int)Math.Round(confidence.Average(), MidpointRounding.AwayFromZero));
        })
        .ToArray();

    private static string ResolveSimpleV1Decision(StepTrackingBenchmarkSummaryRow trial)
    {
        var values = trial.ShadowAssessment
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Contains(CounterRecoveryShadowLabels.BlockedHardShake, StringComparer.Ordinal))
            return SimpleStepDecisionCodes.Blocked;
        if (values.Contains(CounterRecoveryShadowLabels.MotionSupportPresent, StringComparer.Ordinal))
            return SimpleStepDecisionCodes.Supported;
        if (values.Contains(CounterRecoveryShadowLabels.InsufficientMotionEvidence, StringComparer.Ordinal))
            return SimpleStepDecisionCodes.InsufficientEvidence;
        return SimpleStepDecisionCodes.Suspicious;
    }

    private static long ToMilliseconds(long nanoseconds) =>
        Math.Max(0, nanoseconds) / 1_000_000L;
}

public sealed record MotionClassificationCount(string Classification, int WindowCount);

public static class SimpleTemporalReplayCsvExporter
{
    public static void Export(
        string path,
        IReadOnlyList<SimpleTemporalReplayRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var properties = typeof(SimpleTemporalReplayRow).GetProperties();
        using var writer = new StreamWriter(path, append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(string.Join(',', properties.Select(x =>
            Csv(JsonNamingPolicy.CamelCase.ConvertName(x.Name)))));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(',', properties.Select(property =>
                Csv(Format(property.GetValue(row))))));
        }
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
