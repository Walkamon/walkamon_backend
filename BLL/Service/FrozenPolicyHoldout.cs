using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BLL.Service;

public static class HoldoutRecordTypes
{
    public const string TrialMetadata = "holdout_trial_metadata";
    public const string TrialFinalization = "holdout_trial_finalization";
    public const string CollectionLock = "holdout_collection_lock";
}

public static class HoldoutTechnicalValidity
{
    public const string Valid = "VALID";
    public const string InvalidTechnical = "INVALID_TECHNICAL";
    public const string Incomplete = "INCOMPLETE";
}

public static class FrozenHoldoutPolicyIds
{
    public const string Duration4Seconds = "HOLDOUT_DURATION_4S";
    public const string Duration17SecondsOrRegions2 =
        "HOLDOUT_DURATION17S_OR_REGIONS2";
}

public sealed record FrozenHoldoutPolicyDefinition(
    string PolicyId,
    long DurationMs,
    int? RegionCount);

public static class FrozenHoldoutPolicies
{
    public const string EvaluationVersion = "simple-v2-frozen-holdout-v1";
    public const long PolicyADurationMs = 4_000;
    public const long PolicyBDurationMs = 17_000;
    public const int PolicyBRegionCount = 2;

    public static readonly IReadOnlyList<FrozenHoldoutPolicyDefinition> All =
        Array.AsReadOnly<FrozenHoldoutPolicyDefinition>(
    [
        new(FrozenHoldoutPolicyIds.Duration4Seconds, PolicyADurationMs, null),
        new(FrozenHoldoutPolicyIds.Duration17SecondsOrRegions2,
            PolicyBDurationMs, PolicyBRegionCount)
    ]);

    public static IReadOnlyList<HoldoutPolicyTrialResult> Evaluate(
        HoldoutTrialRow trial,
        string policyManifestHash)
    {
        ArgumentNullException.ThrowIfNull(trial);
        if (trial.TechnicalValidity != HoldoutTechnicalValidity.Valid)
            return [];
        if (!trial.CounterDelta.HasValue ||
            !trial.MaxFraudRegionDurationMs.HasValue ||
            !trial.FraudRegionCount.HasValue)
            throw new InvalidOperationException(
                $"Valid holdout trial '{trial.TrialId}' is missing frozen-policy evidence.");

        return All.Select(policy =>
        {
            var block = trial.MaxFraudRegionDurationMs.Value >= policy.DurationMs ||
                        policy.RegionCount.HasValue &&
                        trial.FraudRegionCount.Value >= policy.RegionCount.Value;
            var decision = block
                ? SimpleTemporalPolicyDecisions.Block
                : SimpleTemporalPolicyDecisions.Allow;
            var simulated = block ? 0 : Math.Max(0, trial.CounterDelta.Value);
            var error = simulated - trial.GroundTruthSteps;
            return new HoldoutPolicyTrialResult(
                policy.PolicyId,
                policyManifestHash,
                trial.TrialId,
                trial.PlannedSlotId,
                trial.DeviceId,
                trial.DeviceModel,
                trial.Manufacturer,
                trial.AndroidVersion,
                trial.ApiLevel,
                trial.Scenario,
                decision,
                trial.GroundTruthSteps,
                trial.CounterDelta.Value,
                simulated,
                trial.MaxFraudRegionDurationMs.Value,
                trial.FraudCoverageRatio ?? 0,
                trial.FraudRegionCount.Value,
                error,
                Math.Abs(error));
        }).ToArray();
    }
}

public sealed record FrozenHoldoutPolicyManifest(
    string EvaluationVersion,
    IReadOnlyList<FrozenHoldoutPolicyDefinition> Policies,
    string MotionPolicyRevision,
    string CodeCommitHash,
    string CodeBinaryHash,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> ExpectedTrialIds,
    string PolicyManifestHash)
{
    public static FrozenHoldoutPolicyManifest Create(
        string motionPolicyRevision,
        string codeCommitHash,
        string codeBinaryHash,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motionPolicyRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeCommitHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeBinaryHash);
        var canonicalCreatedAt = AsUtc(createdAtUtc);
        var expected = HoldoutTrialMatrix.ExpectedTrialIds;
        var hash = ComputeHash(
            FrozenHoldoutPolicies.EvaluationVersion,
            FrozenHoldoutPolicies.All,
            motionPolicyRevision.Trim(),
            codeCommitHash.Trim(),
            codeBinaryHash.Trim(),
            canonicalCreatedAt,
            expected);
        return new(
            FrozenHoldoutPolicies.EvaluationVersion,
            FrozenHoldoutPolicies.All,
            motionPolicyRevision.Trim(),
            codeCommitHash.Trim(),
            codeBinaryHash.Trim(),
            canonicalCreatedAt,
            expected,
            hash);
    }

    public void ValidateFrozen()
    {
        if (EvaluationVersion != FrozenHoldoutPolicies.EvaluationVersion ||
            Policies.Count != 2 ||
            Policies[0] != FrozenHoldoutPolicies.All[0] ||
            Policies[1] != FrozenHoldoutPolicies.All[1])
            throw new InvalidDataException(
                "Holdout policy manifest does not contain the exact frozen A/B policies.");
        if (!ExpectedTrialIds.SequenceEqual(
                HoldoutTrialMatrix.ExpectedTrialIds,
                StringComparer.Ordinal))
            throw new InvalidDataException("Holdout trial matrix was modified after freeze.");
        var expectedHash = ComputeHash(
            EvaluationVersion,
            Policies,
            MotionPolicyRevision,
            CodeCommitHash,
            CodeBinaryHash,
            AsUtc(CreatedAtUtc),
            ExpectedTrialIds);
        if (!string.Equals(
                expectedHash,
                PolicyManifestHash,
                StringComparison.Ordinal))
            throw new InvalidDataException("Holdout policy manifest hash is invalid.");
    }

    private static string ComputeHash(
        string evaluationVersion,
        IReadOnlyList<FrozenHoldoutPolicyDefinition> policies,
        string motionPolicyRevision,
        string codeCommitHash,
        string codeBinaryHash,
        DateTime createdAtUtc,
        IReadOnlyList<string> expectedTrialIds)
    {
        var canonical = new StringBuilder()
            .Append(evaluationVersion).Append('|')
            .Append(motionPolicyRevision).Append('|')
            .Append(codeCommitHash).Append('|')
            .Append(codeBinaryHash).Append('|')
            .Append(createdAtUtc.ToString("O", CultureInfo.InvariantCulture));
        foreach (var policy in policies)
        {
            canonical.Append("\nP:")
                .Append(policy.PolicyId).Append(':')
                .Append(policy.DurationMs.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(policy.RegionCount?.ToString(CultureInfo.InvariantCulture) ?? "null");
        }
        foreach (var trial in expectedTrialIds)
            canonical.Append("\nT:").Append(trial);
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public static class HoldoutTrialMatrix
{
    public static readonly IReadOnlyList<string> Scenarios =
        Array.AsReadOnly<string>(
    [
        "NORMAL_HAND", "SLOW_HAND", "FAST_HAND",
        "NORMAL_POCKET", "SLOW_POCKET",
        "SHAKE_LIGHT", "SHAKE_HARD"
    ]);

    public static readonly IReadOnlyList<string> ExpectedTrialIds =
        Array.AsReadOnly((from device in new[] { "d1", "d2" }
         from scenario in Scenarios
         from repetition in new[] { 1, 2 }
         select $"ho-{device}-{scenario.ToLowerInvariant().Replace('_', '-')}-{repetition:00}")
        .ToArray());

    public static string PlannedSlot(string trialId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trialId);
        var normalized = trialId.Trim().ToLowerInvariant();
        var replacement = normalized.LastIndexOf('r');
        if (replacement > 0 &&
            int.TryParse(normalized[(replacement + 1)..], out var replacementNumber) &&
            replacementNumber > 0)
            normalized = normalized[..replacement];
        if (!ExpectedTrialIds.Contains(normalized, StringComparer.Ordinal))
            throw new ArgumentException(
                $"Trial id '{trialId}' is not a planned holdout slot or replacement.");
        return normalized;
    }

    public static string ScenarioFromSlot(string plannedSlot)
    {
        foreach (var scenario in Scenarios)
        {
            if (plannedSlot.Contains(
                    $"-{scenario.ToLowerInvariant().Replace('_', '-')}-",
                    StringComparison.Ordinal))
                return scenario;
        }
        throw new ArgumentException($"Cannot derive scenario from '{plannedSlot}'.");
    }

    public static string DeviceFromSlot(string plannedSlot) =>
        plannedSlot.StartsWith("ho-d1-", StringComparison.Ordinal) ? "d1" :
        plannedSlot.StartsWith("ho-d2-", StringComparison.Ordinal) ? "d2" :
        throw new ArgumentException($"Cannot derive device from '{plannedSlot}'.");

    public static int GroundTruth(string scenario) =>
        scenario is "SHAKE_LIGHT" or "SHAKE_HARD" ? 0 : 100;

    public static string PhonePosition(string scenario) =>
        scenario.EndsWith("_POCKET", StringComparison.Ordinal) ? "POCKET" : "HAND";

    public static string WalkingSpeed(string scenario) => scenario switch
    {
        "SLOW_HAND" or "SLOW_POCKET" => "SLOW",
        "FAST_HAND" => "FAST",
        "NORMAL_HAND" or "NORMAL_POCKET" => "NORMAL",
        _ => "NA"
    };
}

public sealed record HoldoutTrialMetadata(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string PolicyManifestHash,
    string TrialId,
    string PlannedSlotId,
    Guid SessionId,
    string DeviceId,
    string DeviceModel,
    string Manufacturer,
    string AndroidVersion,
    int ApiLevel,
    string Scenario,
    int GroundTruthSteps,
    string PhonePosition,
    string WalkingSpeed,
    int RawDetectorCallbackBaseline,
    DateTime StartedAtUtc);

public sealed record HoldoutTrialFinalization(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string PolicyManifestHash,
    string TrialId,
    string PlannedSlotId,
    Guid SessionId,
    DateTime EndedAtUtc,
    int DurationSeconds,
    int? RawDetectorCallbacks,
    int? DetectorPersisted,
    int? DetectorUploaded,
    long? CounterStart,
    long? CounterEnd,
    int? CounterDelta,
    int? MotionWindowCount,
    int? MotionAccepted,
    int? MotionSuspicious,
    int? MotionRejected,
    int? FraudRegionCount,
    long? FraudDurationMs,
    long? IntervalDurationMs,
    decimal? FraudCoverageRatio,
    long? MaxFraudRegionDurationMs,
    int? HardShakeRegionCount,
    int ServiceRestartCount,
    string TechnicalValidity,
    string? InvalidReason,
    string EvidenceFingerprint,
    int AuthoritativeSteps,
    int RewardDelta,
    int ExpDelta,
    int PvpDelta);

public sealed record HoldoutCollectionLock(
    string RecordType,
    int SchemaVersion,
    string DedupeKey,
    DateTime RecordedAtUtc,
    string PolicyManifestHash,
    int ValidTrialCount,
    int InvalidTrialCount,
    string DatasetFingerprint);

public sealed record HoldoutTrialRow(
    string PolicyManifestHash,
    string TrialId,
    string PlannedSlotId,
    Guid SessionId,
    string DeviceId,
    string DeviceModel,
    string Manufacturer,
    string AndroidVersion,
    int ApiLevel,
    string Scenario,
    int GroundTruthSteps,
    string PhonePosition,
    string WalkingSpeed,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int? DurationSeconds,
    int? RawDetectorCallbacks,
    int? DetectorPersisted,
    int? DetectorUploaded,
    long? CounterStart,
    long? CounterEnd,
    int? CounterDelta,
    int? MotionWindowCount,
    int? MotionAccepted,
    int? MotionSuspicious,
    int? MotionRejected,
    int? FraudRegionCount,
    long? FraudDurationMs,
    long? IntervalDurationMs,
    decimal? FraudCoverageRatio,
    long? MaxFraudRegionDurationMs,
    int? HardShakeRegionCount,
    int? ServiceRestartCount,
    string TechnicalValidity,
    string? InvalidReason,
    string? EvidenceFingerprint);

public sealed record HoldoutPolicyTrialResult(
    string PolicyId,
    string PolicyManifestHash,
    string TrialId,
    string PlannedSlotId,
    string DeviceId,
    string DeviceModel,
    string Manufacturer,
    string AndroidVersion,
    int ApiLevel,
    string Scenario,
    string Decision,
    int GroundTruthSteps,
    int CounterDelta,
    int SimulatedSteps,
    long MaxFraudRegionDurationMs,
    decimal FraudCoverageRatio,
    int FraudRegionCount,
    int Error,
    int AbsoluteError);

public sealed record HoldoutPolicySummary(
    string PolicyId,
    string PolicyManifestHash,
    int ValidTrials,
    int InvalidTrials,
    int WalkingTrials,
    int WalkingAllowed,
    int WalkingBlocked,
    decimal WalkingFalseBlockRate,
    int WalkingGroundTruthSteps,
    long WalkingSimulatedSteps,
    decimal WalkingRetentionRate,
    decimal WalkingMae,
    decimal WalkingMedianAbsoluteError,
    decimal WalkingMeanError,
    long WalkingUnderCountSteps,
    long WalkingOverCountSteps,
    int ShakeTrials,
    int ShakeAllowed,
    int ShakeBlocked,
    decimal ShakeFalseAllowRate,
    long FalseAllowedShakeSteps,
    int ShakeLightAllowed,
    int ShakeLightBlocked,
    long ShakeLightFalseSteps,
    int ShakeHardAllowed,
    int ShakeHardBlocked,
    long ShakeHardFalseSteps,
    decimal NormalHandMae,
    decimal SlowHandMae,
    decimal FastHandMae,
    decimal NormalPocketMae,
    decimal SlowPocketMae);

public sealed record HoldoutEvaluation(
    IReadOnlyList<HoldoutPolicyTrialResult> Results,
    IReadOnlyList<HoldoutPolicySummary> Summaries);

public static class FrozenHoldoutEvaluator
{
    public static HoldoutEvaluation Evaluate(
        IReadOnlyList<HoldoutTrialRow> trials,
        FrozenHoldoutPolicyManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(trials);
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.ValidateFrozen();
        var valid = trials
            .Where(x => x.TechnicalValidity == HoldoutTechnicalValidity.Valid)
            .OrderBy(x => x.TrialId, StringComparer.Ordinal)
            .ToArray();
        var invalidCount = trials.Count(x =>
            x.TechnicalValidity == HoldoutTechnicalValidity.InvalidTechnical);
        var results = valid
            .SelectMany(x => FrozenHoldoutPolicies.Evaluate(
                x, manifest.PolicyManifestHash))
            .OrderBy(x => x.PolicyId, StringComparer.Ordinal)
            .ThenBy(x => x.TrialId, StringComparer.Ordinal)
            .ToArray();
        var summaries = FrozenHoldoutPolicies.All.Select(policy =>
        {
            var rows = results.Where(x => x.PolicyId == policy.PolicyId).ToArray();
            var walking = rows.Where(x => x.GroundTruthSteps > 0).ToArray();
            var shake = rows.Where(x => x.GroundTruthSteps == 0).ToArray();
            var light = shake.Where(x => x.Scenario == "SHAKE_LIGHT").ToArray();
            var hard = shake.Where(x => x.Scenario == "SHAKE_HARD").ToArray();
            var walkingGt = walking.Sum(x => x.GroundTruthSteps);
            var walkingSteps = walking.Sum(x => (long)x.SimulatedSteps);
            return new HoldoutPolicySummary(
                policy.PolicyId,
                manifest.PolicyManifestHash,
                valid.Length,
                invalidCount,
                walking.Length,
                Count(walking, SimpleTemporalPolicyDecisions.Allow),
                Count(walking, SimpleTemporalPolicyDecisions.Block),
                Ratio(Count(walking, SimpleTemporalPolicyDecisions.Block), walking.Length),
                walkingGt,
                walkingSteps,
                walkingGt == 0 ? 0 : Math.Round((decimal)walkingSteps / walkingGt, 6),
                Mean(walking.Select(x => x.AbsoluteError)),
                Median(walking.Select(x => x.AbsoluteError)),
                Mean(walking.Select(x => x.Error)),
                walking.Where(x => x.Error < 0).Sum(x => (long)-x.Error),
                walking.Where(x => x.Error > 0).Sum(x => (long)x.Error),
                shake.Length,
                Count(shake, SimpleTemporalPolicyDecisions.Allow),
                Count(shake, SimpleTemporalPolicyDecisions.Block),
                Ratio(Count(shake, SimpleTemporalPolicyDecisions.Allow), shake.Length),
                FalseSteps(shake),
                Count(light, SimpleTemporalPolicyDecisions.Allow),
                Count(light, SimpleTemporalPolicyDecisions.Block),
                FalseSteps(light),
                Count(hard, SimpleTemporalPolicyDecisions.Allow),
                Count(hard, SimpleTemporalPolicyDecisions.Block),
                FalseSteps(hard),
                ScenarioMae(walking, "NORMAL_HAND"),
                ScenarioMae(walking, "SLOW_HAND"),
                ScenarioMae(walking, "FAST_HAND"),
                ScenarioMae(walking, "NORMAL_POCKET"),
                ScenarioMae(walking, "SLOW_POCKET"));
        }).ToArray();
        return new(results, summaries);
    }

    private static int Count(
        IEnumerable<HoldoutPolicyTrialResult> rows,
        string decision) => rows.Count(x => x.Decision == decision);

    private static long FalseSteps(IEnumerable<HoldoutPolicyTrialResult> rows) => rows
        .Where(x => x.Decision == SimpleTemporalPolicyDecisions.Allow)
        .Sum(x => (long)x.CounterDelta);

    private static decimal ScenarioMae(
        IEnumerable<HoldoutPolicyTrialResult> rows,
        string scenario) => Mean(rows
        .Where(x => x.Scenario == scenario)
        .Select(x => x.AbsoluteError));

    private static decimal Ratio(int numerator, int denominator) => denominator == 0
        ? 0
        : Math.Round((decimal)numerator / denominator, 6);

    private static decimal Mean(IEnumerable<int> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0
            ? 0
            : Math.Round(materialized.Average(x => (decimal)x), 6);
    }

    private static decimal Median(IEnumerable<int> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return 0;
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : Math.Round((ordered[middle - 1] + ordered[middle]) / 2m, 6);
    }
}

public static class HoldoutTrialRowBuilder
{
    public static IReadOnlyList<HoldoutTrialRow> Build(
        IReadOnlyList<HoldoutTrialMetadata> metadata,
        IReadOnlyList<HoldoutTrialFinalization> finalizations)
    {
        var finals = finalizations
            .GroupBy(x => x.TrialId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.RecordedAtUtc).Last(),
                StringComparer.Ordinal);
        return metadata
            .GroupBy(x => x.TrialId, StringComparer.Ordinal)
            .Select(x => x.OrderBy(y => y.RecordedAtUtc).Last())
            .OrderBy(x => x.PlannedSlotId, StringComparer.Ordinal)
            .ThenBy(x => x.TrialId, StringComparer.Ordinal)
            .Select(item =>
            {
                finals.TryGetValue(item.TrialId, out var final);
                return new HoldoutTrialRow(
                    item.PolicyManifestHash,
                    item.TrialId,
                    item.PlannedSlotId,
                    item.SessionId,
                    item.DeviceId,
                    item.DeviceModel,
                    item.Manufacturer,
                    item.AndroidVersion,
                    item.ApiLevel,
                    item.Scenario,
                    item.GroundTruthSteps,
                    item.PhonePosition,
                    item.WalkingSpeed,
                    item.StartedAtUtc,
                    final?.EndedAtUtc,
                    final?.DurationSeconds,
                    final?.RawDetectorCallbacks,
                    final?.DetectorPersisted,
                    final?.DetectorUploaded,
                    final?.CounterStart,
                    final?.CounterEnd,
                    final?.CounterDelta,
                    final?.MotionWindowCount,
                    final?.MotionAccepted,
                    final?.MotionSuspicious,
                    final?.MotionRejected,
                    final?.FraudRegionCount,
                    final?.FraudDurationMs,
                    final?.IntervalDurationMs,
                    final?.FraudCoverageRatio,
                    final?.MaxFraudRegionDurationMs,
                    final?.HardShakeRegionCount,
                    final?.ServiceRestartCount,
                    final?.TechnicalValidity ?? HoldoutTechnicalValidity.Incomplete,
                    final?.InvalidReason,
                    final?.EvidenceFingerprint);
            }).ToArray();
    }
}

public static class HoldoutCsvExporter
{
    public static void Export<T>(string path, IReadOnlyList<T> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        using var writer = new StreamWriter(
            path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(string.Join(',', properties.Select(x => Csv(Camel(x.Name)))));
        foreach (var row in rows)
            writer.WriteLine(string.Join(',', properties.Select(x =>
                Csv(Format(x.GetValue(row))))));
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Camel(string value) =>
        char.ToLowerInvariant(value[0]) + value[1..];

    private static string Csv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}

public static class HoldoutReportBuilder
{
    public static string Build(
        FrozenHoldoutPolicyManifest manifest,
        IReadOnlyList<HoldoutTrialRow> trials,
        HoldoutEvaluation evaluation)
    {
        var builder = new StringBuilder()
            .AppendLine("# Walkamon Frozen Policy Holdout Validation")
            .AppendLine()
            .AppendLine($"Manifest hash: `{manifest.PolicyManifestHash}`")
            .AppendLine($"Motion policy revision: `{manifest.MotionPolicyRevision}`")
            .AppendLine($"Code commit: `{manifest.CodeCommitHash}`")
            .AppendLine($"Code binary hash: `{manifest.CodeBinaryHash}`")
            .AppendLine()
            .AppendLine("## Frozen policies")
            .AppendLine()
            .AppendLine("- A: `maxFraudRegionDurationMs >= 4000` → BLOCK; otherwise ALLOW.")
            .AppendLine("- B: `maxFraudRegionDurationMs >= 17000 OR fraudRegionCount >= 2` → BLOCK; otherwise ALLOW.")
            .AppendLine()
            .AppendLine("## A vs B")
            .AppendLine()
            .AppendLine("| Metric | A | B |")
            .AppendLine("|---|---:|---:|");
        var a = evaluation.Summaries.Single(x =>
            x.PolicyId == FrozenHoldoutPolicyIds.Duration4Seconds);
        var b = evaluation.Summaries.Single(x =>
            x.PolicyId == FrozenHoldoutPolicyIds.Duration17SecondsOrRegions2);
        AppendComparison(builder, "Walking MAE", a.WalkingMae, b.WalkingMae);
        AppendComparison(builder, "Walking false blocks", a.WalkingBlocked, b.WalkingBlocked);
        AppendComparison(builder, "Shake-light false steps", a.ShakeLightFalseSteps, b.ShakeLightFalseSteps);
        AppendComparison(builder, "Shake-hard false steps", a.ShakeHardFalseSteps, b.ShakeHardFalseSteps);
        AppendComparison(builder, "Total false shake steps", a.FalseAllowedShakeSteps, b.FalseAllowedShakeSteps);
        AppendComparison(builder, "Normal-hand MAE", a.NormalHandMae, b.NormalHandMae);
        AppendComparison(builder, "Slow-hand MAE", a.SlowHandMae, b.SlowHandMae);
        AppendComparison(builder, "Fast-hand MAE", a.FastHandMae, b.FastHandMae);
        AppendComparison(builder, "Normal-pocket MAE", a.NormalPocketMae, b.NormalPocketMae);
        AppendComparison(builder, "Slow-pocket MAE", a.SlowPocketMae, b.SlowPocketMae);

        builder.AppendLine()
            .AppendLine("## Device breakdown")
            .AppendLine()
            .AppendLine("| Device | Policy | Walking MAE | Walking blocks | Shake allows | False shake steps |")
            .AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var device in trials
                     .Where(x => x.TechnicalValidity == HoldoutTechnicalValidity.Valid)
                     .GroupBy(x => x.DeviceId)
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var trialIds = device.Select(x => x.TrialId).ToHashSet(StringComparer.Ordinal);
            foreach (var policy in FrozenHoldoutPolicies.All)
            {
                var rows = evaluation.Results.Where(x =>
                    x.PolicyId == policy.PolicyId && trialIds.Contains(x.TrialId)).ToArray();
                var walking = rows.Where(x => x.GroundTruthSteps > 0).ToArray();
                var shake = rows.Where(x => x.GroundTruthSteps == 0).ToArray();
                builder.AppendLine(
                    $"| {device.Key} | {policy.PolicyId} | {Mean(walking.Select(x => x.AbsoluteError)):0.###} | " +
                    $"{walking.Count(x => x.Decision == SimpleTemporalPolicyDecisions.Block)} | " +
                    $"{shake.Count(x => x.Decision == SimpleTemporalPolicyDecisions.Allow)} | " +
                    $"{shake.Where(x => x.Decision == SimpleTemporalPolicyDecisions.Allow).Sum(x => x.CounterDelta)} |");
            }
        }

        builder.AppendLine()
            .AppendLine("## Walking subgroup breakdown")
            .AppendLine()
            .AppendLine("| Scenario | Policy | Allowed | Blocked | MAE | Mean Counter error |")
            .AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var scenario in HoldoutTrialMatrix.Scenarios.Where(x => !x.StartsWith("SHAKE", StringComparison.Ordinal)))
        foreach (var policy in FrozenHoldoutPolicies.All)
        {
            var rows = evaluation.Results.Where(x =>
                x.PolicyId == policy.PolicyId && x.Scenario == scenario).ToArray();
            builder.AppendLine(
                $"| {scenario} | {policy.PolicyId} | " +
                $"{rows.Count(x => x.Decision == SimpleTemporalPolicyDecisions.Allow)} | " +
                $"{rows.Count(x => x.Decision == SimpleTemporalPolicyDecisions.Block)} | " +
                $"{Mean(rows.Select(x => x.AbsoluteError)):0.###} | " +
                $"{Mean(rows.Select(x => x.CounterDelta - x.GroundTruthSteps)):0.###} |");
        }

        builder.AppendLine()
            .AppendLine("## Trial validity")
            .AppendLine()
            .AppendLine($"- Planned slots: {HoldoutTrialMatrix.ExpectedTrialIds.Count}")
            .AppendLine($"- Valid trials evaluated: {trials.Count(x => x.TechnicalValidity == HoldoutTechnicalValidity.Valid)}")
            .AppendLine($"- Technical invalid records retained: {trials.Count(x => x.TechnicalValidity == HoldoutTechnicalValidity.InvalidTechnical)}")
            .AppendLine()
            .AppendLine("No weighted winner or product pass/fail tolerance is selected by this report.")
            .AppendLine("No authoritative, proportional, recovered, reward, EXP, mission, achievement or PvP effect is produced.");
        return builder.ToString();
    }

    private static void AppendComparison(StringBuilder builder, string metric, object a, object b) =>
        builder.AppendLine($"| {metric} | {Invariant(a)} | {Invariant(b)} |");

    private static string Invariant(object value) => value is IFormattable formattable
        ? formattable.ToString(null, CultureInfo.InvariantCulture)
        : value.ToString() ?? string.Empty;

    private static decimal Mean(IEnumerable<int> values)
    {
        var rows = values.ToArray();
        return rows.Length == 0 ? 0 : rows.Average(x => (decimal)x);
    }
}

public static class HoldoutEvidenceFingerprint
{
    public static string Compute(HoldoutTrialFinalization finalization)
    {
        var canonical = string.Join('|',
            finalization.PolicyManifestHash,
            finalization.TrialId,
            finalization.SessionId,
            finalization.CounterStart,
            finalization.CounterEnd,
            finalization.CounterDelta,
            finalization.FraudRegionCount,
            finalization.FraudDurationMs,
            finalization.MaxFraudRegionDurationMs,
            finalization.TechnicalValidity,
            finalization.InvalidReason ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string Dataset(
        string manifestHash,
        IReadOnlyList<HoldoutTrialRow> validTrials)
    {
        var canonical = new StringBuilder(manifestHash);
        foreach (var trial in validTrials
                     .OrderBy(x => x.PlannedSlotId, StringComparer.Ordinal))
            canonical.Append('\n').Append(trial.PlannedSlotId).Append(':')
                .Append(trial.TrialId).Append(':').Append(trial.EvidenceFingerprint);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
