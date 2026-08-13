using System.Globalization;
using System.Reflection;
using System.Text;

namespace BLL.Service;

public static class SimpleTemporalPolicyDecisions
{
    public const string Allow = "ALLOW";
    public const string Block = "BLOCK";
    public const string Defer = "DEFER";
}

public static class SimpleTemporalPolicyFamilies
{
    public const string MaxFraudDuration = "MAX_FRAUD_DURATION";
    public const string FraudCoverage = "FRAUD_COVERAGE";
    public const string DurationOrCoverage = "DURATION_OR_COVERAGE";
    public const string RegionCount = "REGION_COUNT";
    public const string SustainedOrRepeated = "SUSTAINED_OR_REPEATED";
    public const string DurationOrCoverageDefer = "DURATION_OR_COVERAGE_DEFER";
    public const string SustainedOrRepeatedDefer = "SUSTAINED_OR_REPEATED_DEFER";
}

public sealed record SimpleTemporalPolicyTrial(
    string TrialId,
    string Scenario,
    string ScenarioGroup,
    int GroundTruth,
    int CounterDelta,
    int DetectorCount,
    int CurrentV3Accepted,
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
    string ActivityDistributionJson)
{
    public decimal MotionAcceptedRatio => Ratio(MotionAccepted, MotionWindowCount);
    public decimal MotionSuspiciousRatio => Ratio(MotionSuspicious, MotionWindowCount);
    public decimal MotionRejectedRatio => Ratio(MotionRejected, MotionWindowCount);

    public static SimpleTemporalPolicyTrial FromReplay(SimpleTemporalReplayRow row) => new(
        row.TrialId,
        row.Scenario,
        ResolveScenarioGroup(row.TrialId, row.Scenario),
        row.GroundTruth,
        row.CounterDelta,
        row.DetectorCount,
        row.CurrentV3Accepted,
        row.MotionWindowCount,
        row.MotionAccepted,
        row.MotionSuspicious,
        row.MotionRejected,
        row.MotionUnavailable,
        row.FraudRegionCount,
        row.FraudDurationMs,
        row.IntervalDurationMs,
        row.FraudCoverageRatio,
        row.HardShakeRegionCount,
        row.MaxFraudRegionDurationMs,
        row.ActivityDistributionJson);

    private static decimal Ratio(int numerator, int denominator) => denominator <= 0
        ? 0
        : Math.Round((decimal)Math.Max(0, numerator) / denominator, 6);

    public static string ResolveScenarioGroup(string trialId, string scenario)
    {
        var normalized = trialId.Trim().ToLowerInvariant();
        foreach (var value in new[]
                 {
                     "normal-hand", "slow-hand", "fast-hand",
                     "normal-pocket", "slow-pocket",
                     "shake-light", "shake-hard"
                 })
        {
            if (normalized.StartsWith(value, StringComparison.Ordinal))
                return value.Replace('-', '_').ToUpperInvariant();
        }
        return string.IsNullOrWhiteSpace(scenario)
            ? "UNKNOWN"
            : scenario.Trim().Replace('-', '_').ToUpperInvariant();
    }
}

public sealed record SimpleTemporalPolicyConfiguration(
    string PolicyId,
    string PolicyFamily,
    long? XDurationMs,
    decimal? YCoverage,
    int? NRegions,
    bool DefersIntermediateFraud);

public sealed record SimpleTemporalPolicyParameterRanges(
    IReadOnlyList<long> DurationMs,
    IReadOnlyList<decimal> Coverage,
    IReadOnlyList<int> RegionCount);

public sealed record SimpleTemporalPolicyTrialResult(
    string PolicyId,
    string PolicyFamily,
    string TrialId,
    string Scenario,
    string ScenarioGroup,
    string Decision,
    int GroundTruth,
    int CounterDelta,
    int? SimulatedSteps,
    long MaxFraudRegionDurationMs,
    decimal FraudCoverageRatio,
    int FraudRegionCount,
    int HardShakeRegionCount,
    int MotionWindowCount,
    int MotionAccepted,
    int MotionSuspicious,
    int MotionRejected,
    decimal MotionAcceptedRatio,
    decimal MotionSuspiciousRatio,
    decimal MotionRejectedRatio,
    int DetectorCount,
    int CurrentV3Accepted,
    string ActivityDistributionJson,
    int? Error,
    int? AbsoluteError);

public sealed record SimpleTemporalPolicySimulationRow(
    string PolicyId,
    string PolicyFamily,
    long? XDurationMs,
    decimal? YCoverage,
    int? NRegions,
    bool DefersIntermediateFraud,
    int WalkingTrialCount,
    int WalkingAllowed,
    int WalkingBlocked,
    int WalkingDeferred,
    int WalkingResolved,
    decimal WalkingFalseBlockRate,
    decimal WalkingDeferRate,
    long WalkingTotalAbsoluteError,
    decimal? WalkingMae,
    decimal? WalkingMeanError,
    decimal? WalkingMedianAbsoluteError,
    decimal? WalkingUnderCountMean,
    decimal? WalkingOverCountMean,
    int TotalWalkingGt,
    long TotalAllowedCounterSteps,
    decimal WalkingRetentionRate,
    int ShakeTrialCount,
    int ShakeAllowed,
    int ShakeBlocked,
    int ShakeDeferred,
    long FalseAllowedShakeSteps,
    int ShakeLightAllowed,
    int ShakeLightBlocked,
    int ShakeLightDeferred,
    long ShakeLightFalseAllowedSteps,
    int ShakeHardAllowed,
    int ShakeHardBlocked,
    int ShakeHardDeferred,
    long ShakeHardFalseAllowedSteps,
    decimal? NormalHandMae,
    decimal? SlowHandMae,
    decimal? FastHandMae,
    decimal? NormalPocketMae,
    decimal? SlowPocketMae,
    bool IsPareto);

public sealed record SimpleTemporalPolicySimulation(
    IReadOnlyList<SimpleTemporalPolicyTrial> Trials,
    SimpleTemporalPolicyParameterRanges ParameterRanges,
    IReadOnlyList<SimpleTemporalPolicyConfiguration> Policies,
    IReadOnlyList<SimpleTemporalPolicySimulationRow> PolicyRows,
    IReadOnlyList<SimpleTemporalPolicyTrialResult> TrialRows);

public static class SimpleTemporalPolicySimulator
{
    public static SimpleTemporalPolicySimulation Simulate(
        IReadOnlyList<SimpleTemporalPolicyTrial> trials)
    {
        ArgumentNullException.ThrowIfNull(trials);
        var canonicalTrials = trials
            .OrderBy(x => x.TrialId, StringComparer.Ordinal)
            .ToArray();
        if (canonicalTrials.Length == 0)
            throw new ArgumentException("At least one replay trial is required.", nameof(trials));

        var ranges = BuildParameterRanges(canonicalTrials);
        var policies = BuildPolicies(ranges);
        var allTrialRows = new List<SimpleTemporalPolicyTrialResult>(
            policies.Count * canonicalTrials.Length);
        var policyRows = new List<SimpleTemporalPolicySimulationRow>(policies.Count);
        foreach (var policy in policies)
        {
            var results = canonicalTrials
                .Select(trial => Evaluate(policy, trial))
                .ToArray();
            allTrialRows.AddRange(results);
            policyRows.Add(Summarize(policy, results));
        }

        var pareto = policyRows
            .Where(candidate => !policyRows.Any(other =>
                !ReferenceEquals(other, candidate) &&
                other.WalkingFalseBlockRate <= candidate.WalkingFalseBlockRate &&
                other.FalseAllowedShakeSteps <= candidate.FalseAllowedShakeSteps &&
                (other.WalkingFalseBlockRate < candidate.WalkingFalseBlockRate ||
                 other.FalseAllowedShakeSteps < candidate.FalseAllowedShakeSteps)))
            .Select(x => x.PolicyId)
            .ToHashSet(StringComparer.Ordinal);
        var finalized = policyRows
            .Select(x => x with { IsPareto = pareto.Contains(x.PolicyId) })
            .OrderBy(x => x.PolicyFamily, StringComparer.Ordinal)
            .ThenBy(x => x.XDurationMs)
            .ThenBy(x => x.YCoverage)
            .ThenBy(x => x.NRegions)
            .ThenBy(x => x.PolicyId, StringComparer.Ordinal)
            .ToArray();

        return new(
            canonicalTrials,
            ranges,
            policies,
            finalized,
            allTrialRows
                .OrderBy(x => x.PolicyId, StringComparer.Ordinal)
                .ThenBy(x => x.TrialId, StringComparer.Ordinal)
                .ToArray());
    }

    public static IReadOnlyList<SimpleTemporalPolicySimulationRow> SafetyFirst(
        IEnumerable<SimpleTemporalPolicySimulationRow> policies) => policies
        .OrderBy(x => x.FalseAllowedShakeSteps)
        .ThenBy(x => x.WalkingFalseBlockRate)
        .ThenBy(x => x.WalkingMae ?? decimal.MaxValue)
        .ThenBy(x => x.PolicyId, StringComparer.Ordinal)
        .ToArray();

    public static SimpleTemporalPolicyTrialResult Evaluate(
        SimpleTemporalPolicyConfiguration policy,
        SimpleTemporalPolicyTrial trial)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(trial);
        var duration = policy.XDurationMs.HasValue &&
            trial.MaxFraudRegionDurationMs >= policy.XDurationMs.Value;
        var coverage = policy.YCoverage.HasValue &&
            trial.FraudCoverageRatio >= policy.YCoverage.Value;
        var repeated = policy.NRegions.HasValue &&
            trial.FraudRegionCount >= policy.NRegions.Value;
        var block = policy.PolicyFamily switch
        {
            SimpleTemporalPolicyFamilies.MaxFraudDuration => duration,
            SimpleTemporalPolicyFamilies.FraudCoverage => coverage,
            SimpleTemporalPolicyFamilies.DurationOrCoverage => duration || coverage,
            SimpleTemporalPolicyFamilies.RegionCount => repeated,
            SimpleTemporalPolicyFamilies.SustainedOrRepeated => duration || repeated,
            SimpleTemporalPolicyFamilies.DurationOrCoverageDefer => duration || coverage,
            SimpleTemporalPolicyFamilies.SustainedOrRepeatedDefer => duration || repeated,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy), policy.PolicyFamily, "Unknown policy family.")
        };
        var decision = block
            ? SimpleTemporalPolicyDecisions.Block
            : policy.DefersIntermediateFraud && trial.FraudRegionCount > 0
                ? SimpleTemporalPolicyDecisions.Defer
                : SimpleTemporalPolicyDecisions.Allow;
        int? simulated = decision switch
        {
            SimpleTemporalPolicyDecisions.Allow => Math.Max(0, trial.CounterDelta),
            SimpleTemporalPolicyDecisions.Block => 0,
            _ => null
        };
        int? error = simulated.HasValue
            ? simulated.Value - trial.GroundTruth
            : null;

        return new(
            policy.PolicyId,
            policy.PolicyFamily,
            trial.TrialId,
            trial.Scenario,
            trial.ScenarioGroup,
            decision,
            trial.GroundTruth,
            trial.CounterDelta,
            simulated,
            trial.MaxFraudRegionDurationMs,
            trial.FraudCoverageRatio,
            trial.FraudRegionCount,
            trial.HardShakeRegionCount,
            trial.MotionWindowCount,
            trial.MotionAccepted,
            trial.MotionSuspicious,
            trial.MotionRejected,
            trial.MotionAcceptedRatio,
            trial.MotionSuspiciousRatio,
            trial.MotionRejectedRatio,
            trial.DetectorCount,
            trial.CurrentV3Accepted,
            trial.ActivityDistributionJson,
            error,
            error.HasValue ? Math.Abs(error.Value) : null);
    }

    private static SimpleTemporalPolicyParameterRanges BuildParameterRanges(
        IReadOnlyList<SimpleTemporalPolicyTrial> trials)
    {
        var durations = WithAboveMaximum(trials
            .Select(x => x.MaxFraudRegionDurationMs)
            .Where(x => x > 0));
        var coverage = WithAboveMaximum(trials
            .Select(x => x.FraudCoverageRatio)
            .Where(x => x > 0));
        var regions = WithAboveMaximum(trials
            .Select(x => x.FraudRegionCount)
            .Where(x => x > 0));
        return new(durations, coverage, regions);
    }

    private static IReadOnlyList<SimpleTemporalPolicyConfiguration> BuildPolicies(
        SimpleTemporalPolicyParameterRanges ranges)
    {
        var result = new List<SimpleTemporalPolicyConfiguration>();
        foreach (var x in ranges.DurationMs)
            result.Add(Policy(SimpleTemporalPolicyFamilies.MaxFraudDuration, x: x));
        foreach (var y in ranges.Coverage)
            result.Add(Policy(SimpleTemporalPolicyFamilies.FraudCoverage, y: y));
        foreach (var x in ranges.DurationMs)
        foreach (var y in ranges.Coverage)
        {
            result.Add(Policy(SimpleTemporalPolicyFamilies.DurationOrCoverage, x, y));
            result.Add(Policy(
                SimpleTemporalPolicyFamilies.DurationOrCoverageDefer,
                x, y, defer: true));
        }
        foreach (var n in ranges.RegionCount)
            result.Add(Policy(SimpleTemporalPolicyFamilies.RegionCount, n: n));
        foreach (var x in ranges.DurationMs)
        foreach (var n in ranges.RegionCount)
        {
            result.Add(Policy(SimpleTemporalPolicyFamilies.SustainedOrRepeated, x, n: n));
            result.Add(Policy(
                SimpleTemporalPolicyFamilies.SustainedOrRepeatedDefer,
                x, n: n, defer: true));
        }
        return result
            .OrderBy(x => x.PolicyId, StringComparer.Ordinal)
            .ToArray();
    }

    private static SimpleTemporalPolicyConfiguration Policy(
        string family,
        long? x = null,
        decimal? y = null,
        int? n = null,
        bool defer = false)
    {
        var id = string.Join("__",
            family,
            x.HasValue ? $"X_{x.Value}" : "X_NULL",
            y.HasValue
                ? $"Y_{y.Value.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', '_')}"
                : "Y_NULL",
            n.HasValue ? $"N_{n.Value}" : "N_NULL");
        return new(id, family, x, y, n, defer);
    }

    private static SimpleTemporalPolicySimulationRow Summarize(
        SimpleTemporalPolicyConfiguration policy,
        IReadOnlyList<SimpleTemporalPolicyTrialResult> rows)
    {
        var walking = rows.Where(x => x.GroundTruth > 0).ToArray();
        var shake = rows.Where(x => x.GroundTruth == 0).ToArray();
        var light = shake.Where(x => x.ScenarioGroup == "SHAKE_LIGHT").ToArray();
        var hard = shake.Where(x => x.ScenarioGroup == "SHAKE_HARD").ToArray();
        var resolvedWalking = walking.Where(x => x.AbsoluteError.HasValue).ToArray();
        var errors = resolvedWalking.Select(x => x.Error!.Value).ToArray();
        var absolute = resolvedWalking.Select(x => x.AbsoluteError!.Value).ToArray();
        var totalWalkingGt = walking.Sum(x => x.GroundTruth);
        var allowedCounter = walking
            .Where(x => x.Decision == SimpleTemporalPolicyDecisions.Allow)
            .Sum(x => (long)x.CounterDelta);

        return new(
            policy.PolicyId,
            policy.PolicyFamily,
            policy.XDurationMs,
            policy.YCoverage,
            policy.NRegions,
            policy.DefersIntermediateFraud,
            walking.Length,
            Count(walking, SimpleTemporalPolicyDecisions.Allow),
            Count(walking, SimpleTemporalPolicyDecisions.Block),
            Count(walking, SimpleTemporalPolicyDecisions.Defer),
            resolvedWalking.Length,
            Ratio(Count(walking, SimpleTemporalPolicyDecisions.Block), walking.Length),
            Ratio(Count(walking, SimpleTemporalPolicyDecisions.Defer), walking.Length),
            absolute.Sum(x => (long)x),
            Mean(absolute),
            Mean(errors),
            Median(absolute),
            Mean(errors.Where(x => x < 0).Select(x => -x)),
            Mean(errors.Where(x => x > 0)),
            totalWalkingGt,
            allowedCounter,
            totalWalkingGt <= 0
                ? 0
                : Math.Round((decimal)allowedCounter / totalWalkingGt, 6),
            shake.Length,
            Count(shake, SimpleTemporalPolicyDecisions.Allow),
            Count(shake, SimpleTemporalPolicyDecisions.Block),
            Count(shake, SimpleTemporalPolicyDecisions.Defer),
            FalseAllowed(shake),
            Count(light, SimpleTemporalPolicyDecisions.Allow),
            Count(light, SimpleTemporalPolicyDecisions.Block),
            Count(light, SimpleTemporalPolicyDecisions.Defer),
            FalseAllowed(light),
            Count(hard, SimpleTemporalPolicyDecisions.Allow),
            Count(hard, SimpleTemporalPolicyDecisions.Block),
            Count(hard, SimpleTemporalPolicyDecisions.Defer),
            FalseAllowed(hard),
            ScenarioMae(walking, "NORMAL_HAND"),
            ScenarioMae(walking, "SLOW_HAND"),
            ScenarioMae(walking, "FAST_HAND"),
            ScenarioMae(walking, "NORMAL_POCKET"),
            ScenarioMae(walking, "SLOW_POCKET"),
            IsPareto: false);
    }

    private static int Count(
        IEnumerable<SimpleTemporalPolicyTrialResult> rows,
        string decision) => rows.Count(x => x.Decision == decision);

    private static long FalseAllowed(IEnumerable<SimpleTemporalPolicyTrialResult> rows) => rows
        .Where(x => x.Decision == SimpleTemporalPolicyDecisions.Allow)
        .Sum(x => (long)Math.Max(0, x.CounterDelta));

    private static decimal? ScenarioMae(
        IEnumerable<SimpleTemporalPolicyTrialResult> rows,
        string scenario) => Mean(rows
        .Where(x => x.ScenarioGroup == scenario && x.AbsoluteError.HasValue)
        .Select(x => x.AbsoluteError!.Value));

    private static decimal Ratio(int numerator, int denominator) => denominator <= 0
        ? 0
        : Math.Round((decimal)numerator / denominator, 6);

    private static decimal? Mean(IEnumerable<int> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0
            ? null
            : Math.Round(materialized.Average(x => (decimal)x), 6);
    }

    private static decimal? Median(IEnumerable<int> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return null;
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : Math.Round((ordered[middle - 1] + ordered[middle]) / 2m, 6);
    }

    private static IReadOnlyList<long> WithAboveMaximum(IEnumerable<long> values)
    {
        var result = values.Distinct().Order().ToList();
        result.Add(result.Count == 0 ? 1 : checked(result[^1] + 1));
        return result.Distinct().Order().ToArray();
    }

    private static IReadOnlyList<int> WithAboveMaximum(IEnumerable<int> values)
    {
        var result = values.Distinct().Order().ToList();
        result.Add(result.Count == 0 ? 1 : checked(result[^1] + 1));
        return result.Distinct().Order().ToArray();
    }

    private static IReadOnlyList<decimal> WithAboveMaximum(IEnumerable<decimal> values)
    {
        var result = values.Distinct().Order().ToList();
        result.Add(result.Count == 0 ? 0.000001m : result[^1] + 0.000001m);
        return result.Distinct().Order().ToArray();
    }
}

public static class SimpleTemporalPolicyCsvExporter
{
    public static void ExportPolicies(
        string path,
        IReadOnlyList<SimpleTemporalPolicySimulationRow> rows) => Export(path, rows);

    public static void ExportTrials(
        string path,
        IReadOnlyList<SimpleTemporalPolicyTrialResult> rows) => Export(path, rows);

    private static void Export<T>(string path, IReadOnlyList<T> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        using var writer = new StreamWriter(
            path,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(string.Join(',', properties.Select(x =>
            Csv(ToCamelCase(x.Name)))));
        foreach (var row in rows)
            writer.WriteLine(string.Join(',', properties.Select(x =>
                Csv(Format(x.GetValue(row))))));
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        bool boolean => boolean ? "true" : "false",
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string ToCamelCase(string value) => value.Length == 0
        ? value
        : char.ToLowerInvariant(value[0]) + value[1..];

    private static string Csv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}

public static class SimpleTemporalPolicyReportBuilder
{
    public static string Build(SimpleTemporalPolicySimulation simulation)
    {
        var safety = SimpleTemporalPolicySimulator.SafetyFirst(simulation.PolicyRows);
        var pareto = safety.Where(x => x.IsPareto).ToArray();
        var zeroZero = safety.Where(x =>
            x.FalseAllowedShakeSteps == 0 && x.WalkingBlocked == 0).ToArray();
        var fullyResolvedZeroZero = zeroZero.Where(x =>
            x.WalkingDeferred == 0 && x.ShakeDeferred == 0).ToArray();
        var builder = new StringBuilder()
            .AppendLine("# Simple Mode v2 Offline Policy Simulation")
            .AppendLine()
            .AppendLine("Dataset: **DEVELOPMENT / EXPLORATORY SET**. It is not a production validation set and does not demonstrate generalization.")
            .AppendLine()
            .AppendLine($"- Trials: {simulation.Trials.Count} ({simulation.Trials.Count(x => x.GroundTruth > 0)} walking, {simulation.Trials.Count(x => x.GroundTruth == 0)} shake)")
            .AppendLine($"- Candidate policies: {simulation.Policies.Count}")
            .AppendLine($"- Pareto configurations: {pareto.Length}")
            .AppendLine($"- Counter candidates in shake trials: {simulation.Trials.Where(x => x.GroundTruth == 0).Sum(x => x.CounterDelta)}")
            .AppendLine()
            .AppendLine("## Parameter ranges")
            .AppendLine()
            .AppendLine($"- Duration X (ms): {string.Join(", ", simulation.ParameterRanges.DurationMs)}")
            .AppendLine($"- Coverage Y: {string.Join(", ", simulation.ParameterRanges.Coverage.Select(x => x.ToString("0.######", CultureInfo.InvariantCulture)))}")
            .AppendLine($"- Region N: {string.Join(", ", simulation.ParameterRanges.RegionCount)}")
            .AppendLine("- Ranges are unique positive values observed in this dataset plus one above-maximum sentinel. They are simulation boundaries, not production constants.")
            .AppendLine()
            .AppendLine("## Pareto frontier (safety-first order)")
            .AppendLine()
            .AppendLine("Pareto uses only walking false-block rate and false-allowed shake steps, exactly as specified. DEFER is therefore not penalized and can make a policy appear Pareto-optimal without resolving trials.")
            .AppendLine()
            .AppendLine("| Policy | Walk block/defer | Walk MAE | False shake steps | Shake A/B/D |")
            .AppendLine("|---|---:|---:|---:|---:|");
        AppendRows(builder, pareto.Take(30));
        if (pareto.Length > 30)
            builder.AppendLine($"\n_Only the first 30 of {pareto.Length} tied/non-dominated configurations are shown; see CSV for all._");

        builder.AppendLine()
            .AppendLine("## Top safety-first configurations")
            .AppendLine()
            .AppendLine("Sort: false shake steps, walking false-block rate, then resolved walking MAE. No weighted score is used.")
            .AppendLine()
            .AppendLine("| Policy | Walk block/defer | Walk MAE | False shake steps | Subgroup MAE N-H/S-H/F-H/N-P/S-P |")
            .AppendLine("|---|---:|---:|---:|---:|");
        foreach (var row in safety.Take(20))
        {
            builder.AppendLine(
                $"| `{row.PolicyId}` | {row.WalkingBlocked}/{row.WalkingDeferred} | {Value(row.WalkingMae)} | {row.FalseAllowedShakeSteps} | " +
                $"{Value(row.NormalHandMae)}/{Value(row.SlowHandMae)}/{Value(row.FastHandMae)}/{Value(row.NormalPocketMae)}/{Value(row.SlowPocketMae)} |");
        }

        builder.AppendLine()
            .AppendLine("## Zero-shake / zero-walking-block exploratory candidates")
            .AppendLine()
            .AppendLine(zeroZero.Length == 0
                ? "No candidate achieved both conditions on this development set."
                : $"{zeroZero.Length} configurations achieved zero false-allowed shake steps and zero blocked walking trials on this same development set. This is candidate generation only; parameters must be frozen and evaluated on new holdout trials.")
            .AppendLine($"Fully resolved configurations among them (no walking/shake DEFER): {fullyResolvedZeroZero.Length}.")
            .AppendLine();
        if (fullyResolvedZeroZero.Length > 0)
        {
            builder.AppendLine("Representative fully resolved configurations:")
                .AppendLine()
                .AppendLine("| Family | X ms | Y coverage | N regions | Walk MAE | False shake steps |")
                .AppendLine("|---|---:|---:|---:|---:|---:|");
            foreach (var row in fullyResolvedZeroZero
                         .GroupBy(x => x.PolicyFamily)
                         .Select(group => group
                             .OrderBy(x => x.WalkingMae ?? decimal.MaxValue)
                             .ThenBy(x => x.PolicyId, StringComparer.Ordinal)
                             .First())
                         .OrderBy(x => x.PolicyFamily, StringComparer.Ordinal))
            {
                builder.AppendLine(
                    $"| {row.PolicyFamily} | {row.XDurationMs?.ToString(CultureInfo.InvariantCulture) ?? ""} | " +
                    $"{row.YCoverage?.ToString("0.######", CultureInfo.InvariantCulture) ?? ""} | " +
                    $"{row.NRegions?.ToString(CultureInfo.InvariantCulture) ?? ""} | {Value(row.WalkingMae)} | {row.FalseAllowedShakeSteps} |");
            }
            builder.AppendLine();
        }
        AppendSpecial(builder, simulation, "normal-pocket-02");
        AppendSpecial(builder, simulation, "slow-pocket-03");
        AppendSpecial(builder, simulation, "shake-light-02");

        builder.AppendLine("## Limitations")
            .AppendLine()
            .AppendLine("- The same 21 trials generated and evaluated the candidate boundaries; this is exploratory analysis, not validation.")
            .AppendLine("- Counter intervals include idle/baseline time, which can dilute fraud coverage.")
            .AppendLine("- Counter has no per-step timing, so no fraud-region step count is inferred.")
            .AppendLine("- DEFER is reported separately and excluded from resolved-trial MAE; it is never treated as zero or accepted.")
            .AppendLine("- Underlying accelerometer, gyro, jerk, gait and motion-score thresholds remain frozen.")
            .AppendLine()
            .AppendLine("## Safety invariants")
            .AppendLine()
            .AppendLine("Offline only; no authoritative decision, production threshold, Counter Recovery, proportional allocation, fake event/timestamp, API/DB migration, reward, EXP, mission, achievement or PvP side effect.");
        return builder.ToString();
    }

    private static void AppendRows(
        StringBuilder builder,
        IEnumerable<SimpleTemporalPolicySimulationRow> rows)
    {
        foreach (var row in rows)
            builder.AppendLine(
                $"| `{row.PolicyId}` | {row.WalkingBlocked}/{row.WalkingDeferred} | {Value(row.WalkingMae)} | {row.FalseAllowedShakeSteps} | " +
                $"{row.ShakeAllowed}/{row.ShakeBlocked}/{row.ShakeDeferred} |");
    }

    private static void AppendSpecial(
        StringBuilder builder,
        SimpleTemporalPolicySimulation simulation,
        string trialId)
    {
        var trial = simulation.Trials.Single(x => x.TrialId == trialId);
        var outcomes = simulation.TrialRows
            .Where(x => x.TrialId == trialId)
            .GroupBy(x => x.Decision)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Count()}");
        var familyOutcomes = simulation.TrialRows
            .Where(x => x.TrialId == trialId)
            .GroupBy(x => x.PolicyFamily)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group =>
                $"{group.Key}: " +
                string.Join('/', new[]
                {
                    $"A={group.Count(x => x.Decision == SimpleTemporalPolicyDecisions.Allow)}",
                    $"B={group.Count(x => x.Decision == SimpleTemporalPolicyDecisions.Block)}",
                    $"D={group.Count(x => x.Decision == SimpleTemporalPolicyDecisions.Defer)}"
                }));
        builder.AppendLine($"## Special: {trialId}")
            .AppendLine()
            .AppendLine($"- Counter={trial.CounterDelta}, maxRegion={trial.MaxFraudRegionDurationMs}ms, coverage={trial.FraudCoverageRatio.ToString("0.######", CultureInfo.InvariantCulture)}, regions={trial.FraudRegionCount}")
            .AppendLine($"- Across all candidates: {string.Join(", ", outcomes)}")
            .AppendLine($"- By family: {string.Join("; ", familyOutcomes)}")
            .AppendLine();
    }

    private static string Value(decimal? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "NA";
}
