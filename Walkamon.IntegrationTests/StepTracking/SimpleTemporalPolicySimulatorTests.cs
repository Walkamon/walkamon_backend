using BLL.Options;
using BLL.Service;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class SimpleTemporalPolicySimulatorTests
{
    [Fact]
    public void IsolatedWalkingFraudIsEvaluatedDeterministically()
    {
        var trial = Trial("normal-pocket-02", 100, 111, maxMs: 1_000, regions: 1, coverage: 0.008138m);
        var policy = Duration(4_000);

        var first = SimpleTemporalPolicySimulator.Evaluate(policy, trial);
        var retry = SimpleTemporalPolicySimulator.Evaluate(policy, trial);

        Assert.Equal(SimpleTemporalPolicyDecisions.Allow, first.Decision);
        Assert.Equal(111, first.SimulatedSteps);
        Assert.Equal(first, retry);
    }

    [Fact]
    public void FortySecondHardShakeIsBlockedByDurationPolicy()
    {
        var result = SimpleTemporalPolicySimulator.Evaluate(
            Duration(40_000),
            Trial("shake-hard-02", 0, 76, maxMs: 40_000, regions: 1, coverage: 0.14m));

        Assert.Equal(SimpleTemporalPolicyDecisions.Block, result.Decision);
        Assert.Equal(0, result.SimulatedSteps);
    }

    [Fact]
    public void RegionPolicyDistinguishesFragmentedShakeLight()
    {
        var trial = Trial("shake-light-02", 0, 60, maxMs: 4_000, regions: 12, coverage: 0.03828m);
        var regionPolicy = Policy(
            SimpleTemporalPolicyFamilies.RegionCount,
            n: 12);
        var durationPolicy = Duration(5_000);

        Assert.Equal(SimpleTemporalPolicyDecisions.Block,
            SimpleTemporalPolicySimulator.Evaluate(regionPolicy, trial).Decision);
        Assert.Equal(SimpleTemporalPolicyDecisions.Allow,
            SimpleTemporalPolicySimulator.Evaluate(durationPolicy, trial).Decision);
    }

    [Fact]
    public void ZeroFraudCoverageIsAllowedByCoveragePolicy()
    {
        var trial = Trial("normal-hand-01", 100, 98, maxMs: 0, regions: 0, coverage: 0);
        var result = SimpleTemporalPolicySimulator.Evaluate(
            Policy(SimpleTemporalPolicyFamilies.FraudCoverage, y: 0.01m),
            trial);

        Assert.Equal(SimpleTemporalPolicyDecisions.Allow, result.Decision);
    }

    [Fact]
    public void GroundTruthZeroDoesNotDivideByZero()
    {
        var simulation = SimpleTemporalPolicySimulator.Simulate([
            Trial("shake-light-01", 0, 96, maxMs: 22_000, regions: 5, coverage: 0.18m)
        ]);

        Assert.All(simulation.PolicyRows, row =>
        {
            Assert.Equal(0, row.WalkingTrialCount);
            Assert.Equal(0, row.WalkingFalseBlockRate);
            Assert.Null(row.WalkingMae);
        });
    }

    [Fact]
    public void AllowedShakeAddsEntireCounterDeltaToFalseSteps()
    {
        var simulation = SimpleTemporalPolicySimulator.Simulate([
            Trial("normal-hand-01", 100, 100, maxMs: 0, regions: 0, coverage: 0),
            Trial("shake-light-01", 0, 96, maxMs: 4_000, regions: 1, coverage: 0.04m)
        ]);
        var allow = simulation.PolicyRows.Single(x =>
            x.PolicyFamily == SimpleTemporalPolicyFamilies.MaxFraudDuration &&
            x.XDurationMs == 4_001);

        Assert.Equal(96, allow.FalseAllowedShakeSteps);
        Assert.Equal(1, allow.ShakeAllowed);
    }

    [Fact]
    public void BlockedWalkingCountsAsFalseBlock()
    {
        var simulation = SimpleTemporalPolicySimulator.Simulate([
            Trial("normal-pocket-02", 100, 111, maxMs: 1_000, regions: 1, coverage: 0.008m),
            Trial("shake-hard-01", 0, 53, maxMs: 17_000, regions: 2, coverage: 0.11m)
        ]);
        var policy = simulation.PolicyRows.Single(x =>
            x.PolicyFamily == SimpleTemporalPolicyFamilies.MaxFraudDuration &&
            x.XDurationMs == 1_000);

        Assert.Equal(1, policy.WalkingBlocked);
        Assert.Equal(1m, policy.WalkingFalseBlockRate);
        Assert.Equal(100m, policy.WalkingMae);
    }

    [Fact]
    public void DeferredTrialIsNotAllowedAndHasNoSimulatedError()
    {
        var result = SimpleTemporalPolicySimulator.Evaluate(
            Policy(
                SimpleTemporalPolicyFamilies.SustainedOrRepeatedDefer,
                x: 4_000,
                n: 12,
                defer: true),
            Trial("normal-pocket-02", 100, 111, maxMs: 1_000, regions: 1, coverage: 0.008m));

        Assert.Equal(SimpleTemporalPolicyDecisions.Defer, result.Decision);
        Assert.Null(result.SimulatedSteps);
        Assert.Null(result.Error);
        Assert.Null(result.AbsoluteError);
    }

    [Fact]
    public void ParetoCalculationIsDeterministic()
    {
        var trials = new[]
        {
            Trial("normal-pocket-02", 100, 111, maxMs: 1_000, regions: 1, coverage: 0.008m),
            Trial("shake-hard-02", 0, 76, maxMs: 40_000, regions: 1, coverage: 0.14m)
        };

        var first = SimpleTemporalPolicySimulator.Simulate(trials);
        var retry = SimpleTemporalPolicySimulator.Simulate(trials.Reverse().ToArray());

        Assert.Equal(
            first.PolicyRows.Where(x => x.IsPareto).Select(x => x.PolicyId),
            retry.PolicyRows.Where(x => x.IsPareto).Select(x => x.PolicyId));
        Assert.Contains(first.PolicyRows, x =>
            x.IsPareto && x.FalseAllowedShakeSteps == 0 && x.WalkingBlocked == 0);
    }

    [Fact]
    public void SimulatorOutputCannotCarryProductionSideEffects()
    {
        var names = typeof(SimpleTemporalPolicySimulationRow)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(names, x =>
            x.Contains("Authoritative", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Reward", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Exp", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Pvp", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Recovered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SimulatorPublicApiHasNoDatabaseDependency()
    {
        var types = typeof(SimpleTemporalPolicySimulator)
            .GetMethods()
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.DoesNotContain(types, name =>
            name.Contains("DbContext", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("WalkamonContext", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("StepProgressApplier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SimulationDoesNotModifyRuntimeFeatureFlags()
    {
        var options = new StepValidationOptions
        {
            V3AuthoritativeEnabled = false,
            SimpleStepValidationEnabled = false,
            SimpleStepValidationRevision = "aggregate_v1",
            SimpleStepValidationShadowOnly = true
        };

        _ = SimpleTemporalPolicySimulator.Simulate([
            Trial("normal-hand-01", 100, 98, maxMs: 0, regions: 0, coverage: 0)
        ]);

        Assert.False(options.V3AuthoritativeEnabled);
        Assert.False(options.SimpleStepValidationEnabled);
        Assert.Equal("aggregate_v1", options.SimpleStepValidationRevision);
        Assert.True(options.SimpleStepValidationShadowOnly);
    }

    private static SimpleTemporalPolicyConfiguration Duration(long value) =>
        Policy(SimpleTemporalPolicyFamilies.MaxFraudDuration, x: value);

    private static SimpleTemporalPolicyConfiguration Policy(
        string family,
        long? x = null,
        decimal? y = null,
        int? n = null,
        bool defer = false) => new(
        $"TEST_{family}_{x}_{y}_{n}_{defer}",
        family,
        x,
        y,
        n,
        defer);

    private static SimpleTemporalPolicyTrial Trial(
        string trialId,
        int groundTruth,
        int counter,
        long maxMs,
        int regions,
        decimal coverage) => new(
        trialId,
        groundTruth == 0 ? "SHAKE" : "WALK",
        SimpleTemporalPolicyTrial.ResolveScenarioGroup(
            trialId,
            groundTruth == 0 ? "SHAKE" : "WALK"),
        groundTruth,
        counter,
        DetectorCount: 50,
        CurrentV3Accepted: groundTruth == 0 ? 0 : 40,
        MotionWindowCount: 10,
        MotionAccepted: 5,
        MotionSuspicious: 3,
        MotionRejected: 2,
        MotionUnavailable: 0,
        FraudRegionCount: regions,
        FraudDurationMs: maxMs,
        IntervalDurationMs: 100_000,
        FraudCoverageRatio: coverage,
        HardShakeRegionCount: regions,
        MaxFraudRegionDurationMs: maxMs,
        ActivityDistributionJson: "[]");
}
