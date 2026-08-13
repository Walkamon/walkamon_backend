using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BLL.Service;

public static class StepTrackingBenchmarkRecordTypes
{
    public const string TrialMetadata = "trial_metadata";
    public const string ShadowInterval = "shadow_interval";
    public const string SimpleShadowInterval = "simple_shadow_interval";
    public const string SimpleTemporalShadowInterval = "simple_temporal_shadow_interval";
    public const string TrialFinalization = "trial_finalization";
}

public sealed record StepTrackingBenchmarkTrialMetadata(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string TrialId,
    Guid SessionId,
    string Scenario,
    int GroundTruthSteps,
    string PhonePosition,
    string ScreenState,
    string WalkingSpeedCategory,
    string? DeviceModel,
    DateTime StartedAtUtc);

public sealed record StepTrackingBenchmarkShadowInterval(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string TrialId,
    Guid SessionId,
    Guid BootSessionId,
    DateTime SessionStartedAtUtc,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterStart,
    long CounterEnd,
    int CounterDelta,
    int DetectorCount,
    int DetectorAccepted,
    int DetectorSuspicious,
    int DetectorRejected,
    int DetectorPending,
    int CounterExcess,
    int ExcessVsAcceptedDetector,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    bool HardShakeMajorityObserved,
    IReadOnlyList<CounterRecoveryActivityDistribution> ActivityDistribution,
    IReadOnlyList<CounterRecoveryGaitDistribution> GaitDistribution,
    string ShadowAssessment,
    string ShadowIntervalId,
    string EvidenceFingerprint,
    int AuthoritativeSteps,
    int RewardDelta,
    int ExpDelta,
    int PvpDelta);

public sealed record StepTrackingBenchmarkActivitySummary(
    string ActivityCode,
    int WindowCount,
    int? MinimumConfidence,
    int? MaximumConfidence,
    decimal? AverageConfidence);

public sealed record StepTrackingBenchmarkSimpleShadowInterval(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string TrialId,
    Guid SessionId,
    Guid BootSessionId,
    Guid StartClientSampleId,
    Guid EndClientSampleId,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterStart,
    long CounterEnd,
    long CounterDelta,
    int DetectorCount,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    int HardShakeBatchCount,
    bool HardShakeObserved,
    IReadOnlyList<SimpleStepActivityDistribution> ActivityDistribution,
    string SimpleDecision,
    long ShadowSimpleSteps,
    IReadOnlyList<string> ReasonCodes,
    string SimpleIntervalId,
    string EvidenceFingerprint,
    bool V3AuthoritativeEnabled,
    int AuthoritativeSteps,
    int RewardDelta,
    int ExpDelta,
    int PvpDelta);

public sealed record StepTrackingBenchmarkSimpleTemporalShadowInterval(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string TrialId,
    Guid SessionId,
    Guid BootSessionId,
    string CounterIntervalId,
    long IntervalStartElapsedNs,
    long IntervalEndElapsedNs,
    long CounterDelta,
    int DetectorCount,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    int FraudRegionCount,
    long FraudDurationMs,
    long IntervalDurationMs,
    decimal FraudCoverageRatio,
    int HardShakeRegionCount,
    long MaxFraudRegionDurationMs,
    long ShadowCounterCandidate,
    IReadOnlyList<SimpleStepActivityDistribution> ActivityDistribution,
    IReadOnlyList<TemporalFraudRegion> FraudRegions,
    string SimpleV2EvidenceClass,
    string EvidenceFingerprint,
    bool Authoritative,
    int AuthoritativeSteps,
    int RewardDelta,
    int ExpDelta,
    int PvpDelta);

public sealed record StepTrackingBenchmarkTrialFinalization(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string TrialId,
    Guid SessionId,
    IReadOnlyList<Guid> BootSessionIds,
    DateTime EndedAtUtc,
    int DurationSeconds,
    int RawDetectorCallbacks,
    int DetectorPersisted,
    int DetectorUploaded,
    int ServiceRestartCount,
    long? CounterStart,
    long? CounterEnd,
    int CounterDelta,
    int CounterExcess,
    int ExcessVsAcceptedDetector,
    int DetectorAccepted,
    int DetectorSuspicious,
    int DetectorRejected,
    int DetectorPending,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    int HardShakeBatchCount,
    bool HardShakeMajorityObserved,
    IReadOnlyList<StepTrackingBenchmarkActivitySummary> ActivityDistribution,
    int AuthoritativeSteps,
    int RewardDelta,
    int ExpDelta,
    int PvpDelta,
    string EvidenceFingerprint);

public sealed record StepTrackingBenchmarkSummaryRow(
    string TrialId,
    Guid SessionId,
    string BootSessionIds,
    string? DeviceModel,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string Scenario,
    int GroundTruthSteps,
    int DurationSeconds,
    string PhonePosition,
    string ScreenState,
    string WalkingSpeedCategory,
    int RawDetectorCallbacks,
    int DetectorPersisted,
    int DetectorUploaded,
    long? CounterStart,
    long? CounterEnd,
    int CounterDelta,
    int CounterExcess,
    int ExcessVsAcceptedDetector,
    int DetectorAccepted,
    int DetectorSuspicious,
    int DetectorRejected,
    int DetectorPending,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    int MotionUnavailable,
    decimal? MotionAcceptedRatio,
    decimal? MotionSuspiciousRatio,
    decimal? MotionRejectedRatio,
    int HardShakeBatchCount,
    bool HardShakeMajorityObserved,
    int GaitAccepted,
    int GaitPartial,
    int GaitLow,
    int GaitUnavailable,
    string ActivityDistributionJson,
    string ShadowAssessment,
    string EvidenceFingerprint,
    int AuthoritativeSteps,
    int RewardDelta,
    int ExpDelta,
    int PvpDelta,
    decimal? DetectorRecallVsGroundTruth,
    int? CounterError,
    int? CounterAbsoluteError,
    decimal? CounterErrorPercent,
    decimal? FinalDetectorAcceptedVsGroundTruth,
    int? FalseDetectorCount,
    int? FalseCounterCount,
    int? FalseAcceptedCount,
    int ServiceRestartCount);

public sealed class StepTrackingBenchmarkArtifactStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private readonly string _directory;
    private readonly string _jsonlPath;
    private readonly string _lockPath;

    public StepTrackingBenchmarkArtifactStore(
        string artifactDirectory,
        string jsonlFileName = "step-benchmark.jsonl")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonlFileName);
        _directory = Path.GetFullPath(artifactDirectory);
        _jsonlPath = Path.Combine(_directory, Path.GetFileName(jsonlFileName));
        _lockPath = _jsonlPath + ".lock";
    }

    public string JsonlPath => _jsonlPath;

    public async Task<bool> AppendIfNewAsync<T>(
        T record,
        string dedupeKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dedupeKey);
        Directory.CreateDirectory(_directory);
        await using var artifactLock = await AcquireLockAsync(cancellationToken);
        if (await ContainsDedupeKeyAsync(dedupeKey, cancellationToken))
            return false;

        var json = JsonSerializer.Serialize(record, JsonOptions);
        await using var stream = new FileStream(
            _jsonlPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
        return true;
    }

    public IReadOnlyList<JsonDocument> ReadAll()
    {
        if (!File.Exists(_jsonlPath)) return [];
        var records = new List<JsonDocument>();
        foreach (var line in File.ReadLines(_jsonlPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                records.Add(JsonDocument.Parse(line));
            }
            catch (JsonException)
            {
                // A malformed/truncated line is ignored; append-only records before
                // it remain usable and the exporter stays deterministic.
            }
        }
        return records;
    }

    public StepTrackingBenchmarkTrialMetadata? FindTrial(Guid sessionId) =>
        ReadTyped<StepTrackingBenchmarkTrialMetadata>(
                StepTrackingBenchmarkRecordTypes.TrialMetadata)
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.RecordedAtUtc)
            .LastOrDefault();

    public IReadOnlyList<T> ReadTyped<T>(string recordType)
    {
        var result = new List<T>();
        foreach (var document in ReadAll())
        {
            using (document)
            {
                var root = document.RootElement;
                if (!root.TryGetProperty("recordType", out var type) ||
                    !string.Equals(type.GetString(), recordType, StringComparison.Ordinal))
                    continue;
                var value = root.Deserialize<T>(JsonOptions);
                if (value != null) result.Add(value);
            }
        }
        return result;
    }

    private async Task<bool> ContainsDedupeKeyAsync(
        string dedupeKey,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_jsonlPath)) return false;
        using var stream = new FileStream(
            _jsonlPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("dedupeKey", out var key) &&
                    string.Equals(key.GetString(), dedupeKey, StringComparison.Ordinal))
                    return true;
            }
            catch (JsonException)
            {
                // See ReadAll: keep valid append-only records usable.
            }
        }
        return false;
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, cancellationToken);
            }
        }
    }
}

public static class StepTrackingBenchmarkSummaryBuilder
{
    public static IReadOnlyList<StepTrackingBenchmarkSummaryRow> Build(
        IReadOnlyList<StepTrackingBenchmarkTrialMetadata> trials,
        IReadOnlyList<StepTrackingBenchmarkShadowInterval> intervals,
        IReadOnlyList<StepTrackingBenchmarkTrialFinalization> finalizations)
    {
        var result = new List<StepTrackingBenchmarkSummaryRow>();
        foreach (var trial in trials
                     .GroupBy(x => x.SessionId)
                     .Select(x => x.OrderBy(y => y.RecordedAtUtc).Last())
                     .OrderBy(x => x.StartedAtUtc)
                     .ThenBy(x => x.TrialId, StringComparer.Ordinal))
        {
            // The shadow evaluator's interval id includes the current counter end.
            // Successive uploads therefore create cumulative snapshots for the
            // same baseline. Keep only the furthest/latest snapshot per
            // session+boot+baseline lineage before aggregating a physical trial.
            var latestIntervals = intervals
                .Where(x => x.SessionId == trial.SessionId)
                .GroupBy(x => new
                {
                    x.SessionId,
                    x.BootSessionId,
                    x.IntervalStartElapsedNs,
                    x.CounterStart
                })
                .Select(x => x
                    .OrderBy(y => y.IntervalEndElapsedNs)
                    .ThenBy(y => y.RecordedAtUtc)
                    .Last())
                .ToArray();
            var finalization = finalizations
                .Where(x => x.SessionId == trial.SessionId)
                .OrderBy(x => x.RecordedAtUtc)
                .LastOrDefault();

            var detectorPersisted = finalization?.DetectorPersisted ??
                latestIntervals.Sum(x => x.DetectorCount);
            var detectorAccepted = finalization?.DetectorAccepted ??
                latestIntervals.Sum(x => x.DetectorAccepted);
            var counterDelta = finalization?.CounterDelta ??
                latestIntervals.Sum(x => x.CounterDelta);
            var groundTruth = trial.GroundTruthSteps;
            var hasCanonicalShadowEvidence = latestIntervals.Length > 0;
            var motionWindowCount = hasCanonicalShadowEvidence
                ? latestIntervals.Sum(x => x.MotionWindowCount)
                : finalization?.MotionWindowCount ?? 0;
            var motionAccepted = hasCanonicalShadowEvidence
                ? latestIntervals.Sum(x => x.MotionAccepted)
                : finalization?.MotionAccepted ?? 0;
            var motionSuspicious = hasCanonicalShadowEvidence
                ? latestIntervals.Sum(x => x.MotionSuspicious)
                : finalization?.MotionSuspicious ?? 0;
            var motionRejected = hasCanonicalShadowEvidence
                ? latestIntervals.Sum(x => x.MotionRejected)
                : finalization?.MotionRejected ?? 0;
            var motionUnavailable = hasCanonicalShadowEvidence
                ? latestIntervals.Sum(x => x.MotionUnavailable)
                : finalization?.MotionUnavailable ?? 0;
            var activity = hasCanonicalShadowEvidence
                ? AggregateActivities(latestIntervals)
                : finalization?.ActivityDistribution ?? [];
            var gait = latestIntervals
                .SelectMany(x => x.GaitDistribution)
                .GroupBy(x => x.GaitStatus, StringComparer.Ordinal)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => y.DetectorCount),
                    StringComparer.Ordinal);

            result.Add(new StepTrackingBenchmarkSummaryRow(
                trial.TrialId,
                trial.SessionId,
                string.Join('|', (finalization?.BootSessionIds ?? latestIntervals
                        .Select(x => x.BootSessionId).ToArray())
                    .Select(x => x.ToString("D"))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)),
                trial.DeviceModel,
                trial.StartedAtUtc,
                finalization?.EndedAtUtc,
                trial.Scenario,
                groundTruth,
                finalization?.DurationSeconds ?? 0,
                trial.PhonePosition,
                trial.ScreenState,
                trial.WalkingSpeedCategory,
                finalization?.RawDetectorCallbacks ?? detectorPersisted,
                detectorPersisted,
                finalization?.DetectorUploaded ?? detectorPersisted,
                finalization?.CounterStart ?? latestIntervals
                    .OrderBy(x => x.RecordedAtUtc).Select(x => (long?)x.CounterStart).FirstOrDefault(),
                finalization?.CounterEnd ?? latestIntervals
                    .OrderBy(x => x.RecordedAtUtc).Select(x => (long?)x.CounterEnd).LastOrDefault(),
                counterDelta,
                finalization?.CounterExcess ?? latestIntervals.Sum(x => x.CounterExcess),
                finalization?.ExcessVsAcceptedDetector ?? Math.Max(0, counterDelta - detectorAccepted),
                detectorAccepted,
                finalization?.DetectorSuspicious ?? latestIntervals.Sum(x => x.DetectorSuspicious),
                finalization?.DetectorRejected ?? latestIntervals.Sum(x => x.DetectorRejected),
                finalization?.DetectorPending ?? latestIntervals.Sum(x => x.DetectorPending),
                motionWindowCount,
                motionAccepted,
                motionSuspicious,
                motionRejected,
                motionUnavailable,
                Ratio(motionAccepted, motionWindowCount),
                Ratio(motionSuspicious, motionWindowCount),
                Ratio(motionRejected, motionWindowCount),
                finalization?.HardShakeBatchCount ?? 0,
                finalization?.HardShakeMajorityObserved == true || latestIntervals.Any(x => x.HardShakeMajorityObserved),
                gait.GetValueOrDefault("accepted"),
                gait.GetValueOrDefault("partial"),
                gait.GetValueOrDefault("low"),
                gait.GetValueOrDefault("unavailable"),
                JsonSerializer.Serialize(activity.OrderBy(x => x.ActivityCode),
                    StepTrackingBenchmarkArtifactStore.JsonOptions),
                string.Join('|', latestIntervals.Select(x => x.ShadowAssessment)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                string.Join('|', latestIntervals.Select(x => x.EvidenceFingerprint)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                finalization?.AuthoritativeSteps ?? 0,
                finalization?.RewardDelta ?? 0,
                finalization?.ExpDelta ?? 0,
                finalization?.PvpDelta ?? 0,
                groundTruth > 0 ? Ratio(detectorPersisted, groundTruth) : null,
                groundTruth > 0 ? counterDelta - groundTruth : null,
                groundTruth > 0 ? Math.Abs(counterDelta - groundTruth) : null,
                groundTruth > 0
                    ? Math.Round((counterDelta - groundTruth) * 100m / groundTruth, 6)
                    : null,
                groundTruth > 0 ? Ratio(detectorAccepted, groundTruth) : null,
                groundTruth == 0 ? detectorPersisted : null,
                groundTruth == 0 ? counterDelta : null,
                groundTruth == 0 ? detectorAccepted : null,
                finalization?.ServiceRestartCount ?? 0));
        }
        return result;
    }

    private static decimal? Ratio(int numerator, int denominator) =>
        denominator <= 0 ? null : Math.Round((decimal)numerator / denominator, 6);

    private static IReadOnlyList<StepTrackingBenchmarkActivitySummary> AggregateActivities(
        IEnumerable<StepTrackingBenchmarkShadowInterval> intervals) => intervals
        .SelectMany(x => x.ActivityDistribution)
        .GroupBy(x => x.ActivityCode, StringComparer.Ordinal)
        .OrderBy(x => x.Key, StringComparer.Ordinal)
        .Select(group =>
        {
            var count = group.Sum(x => x.WindowCount);
            return new StepTrackingBenchmarkActivitySummary(
                group.Key,
                count,
                group.Min(x => (int?)x.MinimumConfidence),
                group.Max(x => (int?)x.MaximumConfidence),
                count == 0
                    ? null
                    : Math.Round(group.Sum(x => x.AverageConfidence * x.WindowCount) /
                        (decimal)count, 6));
        })
        .ToArray();
}

public static class StepTrackingBenchmarkCsvExporter
{
    public static void Export(
        string path,
        IReadOnlyList<StepTrackingBenchmarkSummaryRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var properties = typeof(StepTrackingBenchmarkSummaryRow).GetProperties();
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
        DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
