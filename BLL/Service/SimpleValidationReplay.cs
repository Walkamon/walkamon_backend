using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BLL.Service;

public sealed record SimpleValidationReplayRow(
    string TrialId,
    string Scenario,
    int GroundTruth,
    int CounterDelta,
    int DetectorCount,
    int CurrentDetectorAccepted,
    string SimpleDecision,
    long ShadowSimpleSteps,
    int CurrentError,
    long SimpleError,
    int CurrentAbsoluteError,
    long SimpleAbsoluteError,
    int CurrentFalseAccepted,
    long SimpleFalseAccepted,
    bool HardShakeObserved,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    string ReplayScope,
    IReadOnlyList<string> ReasonCodes);

public static class SimpleValidationReplayBuilder
{
    public static IReadOnlyList<SimpleValidationReplayRow> Build(
        IReadOnlyList<StepTrackingBenchmarkSummaryRow> trials) => trials
        .OrderBy(x => x.StartedAtUtc)
        .ThenBy(x => x.TrialId, StringComparer.Ordinal)
        .Select(Build)
        .ToArray();

    private static SimpleValidationReplayRow Build(
        StepTrackingBenchmarkSummaryRow trial)
    {
        var counterStart = trial.CounterStart ?? 0;
        var counterEnd = trial.CounterEnd ?? counterStart + trial.CounterDelta;
        var elapsedEnd = Math.Max(1L, trial.DurationSeconds) * 1_000_000_000L;
        var interval = new SimpleCounterInterval(
            DeterministicGuid($"{trial.SessionId:D}:replay:start"),
            DeterministicGuid($"{trial.SessionId:D}:replay:end"),
            ParseFirstBootId(trial.BootSessionIds, trial.SessionId),
            0,
            elapsedEnd,
            counterStart,
            counterEnd,
            trial.CounterDelta);
        var activities = ParseActivities(trial.ActivityDistributionJson);
        var reasons = activities
            .Select(x => x.ActivityCode switch
            {
                "still" => "activity_still",
                "vehicle" or "in_vehicle" => "activity_vehicle",
                "bicycle" or "on_bicycle" => "activity_bicycle",
                _ => null
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToArray();
        var assessment = SimpleStepIntervalEvaluator.Evaluate(new(
            trial.SessionId,
            interval,
            trial.DetectorPersisted,
            trial.MotionWindowCount,
            trial.MotionAccepted,
            trial.MotionSuspicious,
            trial.MotionRejected,
            trial.MotionUnavailable,
            trial.HardShakeBatchCount,
            trial.HardShakeMajorityObserved,
            activities,
            reasons));
        var currentError = trial.DetectorAccepted - trial.GroundTruthSteps;
        var simpleError = assessment.ShadowSimpleSteps - trial.GroundTruthSteps;
        return new(
            trial.TrialId,
            trial.Scenario,
            trial.GroundTruthSteps,
            trial.CounterDelta,
            trial.DetectorPersisted,
            trial.DetectorAccepted,
            assessment.SimpleDecision,
            assessment.ShadowSimpleSteps,
            currentError,
            simpleError,
            Math.Abs(currentError),
            Math.Abs(simpleError),
            trial.GroundTruthSteps == 0 ? trial.DetectorAccepted : 0,
            trial.GroundTruthSteps == 0 ? assessment.ShadowSimpleSteps : 0,
            trial.HardShakeMajorityObserved,
            trial.MotionWindowCount,
            trial.MotionAccepted,
            trial.MotionSuspicious,
            trial.MotionRejected,
            "trial_aggregate",
            assessment.ReasonCodes);
    }

    private static IReadOnlyList<SimpleStepActivityDistribution> ParseActivities(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<StepTrackingBenchmarkActivitySummary[]>(
                        json,
                        StepTrackingBenchmarkArtifactStore.JsonOptions) ?? [])
                .Select(x => new SimpleStepActivityDistribution(
                    x.ActivityCode.Trim().ToLowerInvariant(),
                    x.WindowCount,
                    x.MinimumConfidence ?? 0,
                    x.MaximumConfidence ?? 0,
                    (int)Math.Round(
                        x.AverageConfidence ?? 0,
                        MidpointRounding.AwayFromZero)))
                .OrderBy(x => x.ActivityCode, StringComparer.Ordinal)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Guid ParseFirstBootId(string value, Guid sessionId)
    {
        var first = value.Split('|', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries).FirstOrDefault();
        return Guid.TryParse(first, out var parsed)
            ? parsed
            : DeterministicGuid($"{sessionId:D}:replay:boot");
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

public static class SimpleValidationReplayCsvExporter
{
    public static void Export(
        string path,
        IReadOnlyList<SimpleValidationReplayRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var properties = typeof(SimpleValidationReplayRow).GetProperties();
        using var writer = new StreamWriter(path, append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(string.Join(',', properties.Select(x =>
            Csv(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(x.Name)))));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(',', properties.Select(property =>
                Csv(Format(property.GetValue(row))))));
        }
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        IReadOnlyList<string> values => string.Join('|', values),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
