using BLL.Service;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class FrozenPolicyHoldoutTests
{
    private static readonly FrozenHoldoutPolicyManifest Manifest =
        FrozenHoldoutPolicyManifest.Create(
            "motion-v2-frozen",
            "0123456789abcdef",
            "binary-hash",
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc));

    [Theory]
    [InlineData(3_999, "ALLOW")]
    [InlineData(4_000, "BLOCK")]
    public void PolicyAHasFrozenInclusiveFourSecondBoundary(long duration, string decision)
    {
        var result = Evaluate(Trial(maxDurationMs: duration, regions: 1))
            .Single(x => x.PolicyId == FrozenHoldoutPolicyIds.Duration4Seconds);

        Assert.Equal(decision, result.Decision);
    }

    [Theory]
    [InlineData(16_999, 1, "ALLOW")]
    [InlineData(17_000, 1, "BLOCK")]
    [InlineData(1_000, 2, "BLOCK")]
    [InlineData(1_000, 1, "ALLOW")]
    public void PolicyBHasFrozenDurationOrRegionBoundary(
        long duration,
        int regions,
        string decision)
    {
        var result = Evaluate(Trial(maxDurationMs: duration, regions: regions))
            .Single(x => x.PolicyId == FrozenHoldoutPolicyIds.Duration17SecondsOrRegions2);

        Assert.Equal(decision, result.Decision);
    }

    [Fact]
    public void AllowUsesEntireCounterAggregate()
    {
        var result = Evaluate(Trial(counterDelta: 87, maxDurationMs: 0, regions: 0));

        Assert.All(result, row =>
        {
            Assert.Equal(SimpleTemporalPolicyDecisions.Allow, row.Decision);
            Assert.Equal(87, row.SimulatedSteps);
        });
    }

    [Fact]
    public void BlockUsesZeroWithoutProportionalAllocation()
    {
        var result = Evaluate(Trial(counterDelta: 87, maxDurationMs: 40_000, regions: 1));

        Assert.All(result, row =>
        {
            Assert.Equal(SimpleTemporalPolicyDecisions.Block, row.Decision);
            Assert.Equal(0, row.SimulatedSteps);
        });
    }

    [Fact]
    public void AllowedShakeAddsWholeCounterDeltaToFalseSteps()
    {
        var shake = Trial(
            trialId: "ho-d1-shake-light-01",
            scenario: "SHAKE_LIGHT",
            groundTruth: 0,
            counterDelta: 91,
            maxDurationMs: 1_000,
            regions: 1);
        var evaluation = FrozenHoldoutEvaluator.Evaluate([shake], Manifest);

        Assert.All(evaluation.Summaries, summary =>
        {
            Assert.Equal(1, summary.ShakeAllowed);
            Assert.Equal(91, summary.FalseAllowedShakeSteps);
        });
    }

    [Fact]
    public void BlockedWalkingCountsAsFalseBlock()
    {
        var walking = Trial(maxDurationMs: 40_000, regions: 2);
        var evaluation = FrozenHoldoutEvaluator.Evaluate([walking], Manifest);

        Assert.All(evaluation.Summaries, summary =>
        {
            Assert.Equal(1, summary.WalkingBlocked);
            Assert.Equal(1m, summary.WalkingFalseBlockRate);
            Assert.Equal(100m, summary.WalkingMae);
        });
    }

    [Fact]
    public void TechnicalInvalidTrialIsRetainedButExcludedFromMetrics()
    {
        var invalid = Trial() with
        {
            TechnicalValidity = HoldoutTechnicalValidity.InvalidTechnical,
            InvalidReason = "app_crash",
            CounterDelta = null,
            MaxFraudRegionDurationMs = null,
            FraudRegionCount = null
        };
        var valid = Trial(trialId: "ho-d1-normal-hand-02");
        var evaluation = FrozenHoldoutEvaluator.Evaluate([invalid, valid], Manifest);

        Assert.All(evaluation.Summaries, summary =>
        {
            Assert.Equal(1, summary.InvalidTrials);
            Assert.Equal(1, summary.ValidTrials);
            Assert.Equal(1, summary.WalkingTrials);
        });
        Assert.Equal(2, evaluation.Results.Count);
    }

    [Fact]
    public void ManifestHashIsDeterministic()
    {
        var retry = FrozenHoldoutPolicyManifest.Create(
            "motion-v2-frozen",
            "0123456789abcdef",
            "binary-hash",
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(Manifest.PolicyManifestHash, retry.PolicyManifestHash);
        Manifest.ValidateFrozen();
        retry.ValidateFrozen();
    }

    [Fact]
    public void FrozenPolicyValuesCannotBeMutatedAfterManifestInit()
    {
        var tampered = Manifest with
        {
            Policies =
            [
                new(FrozenHoldoutPolicyIds.Duration4Seconds, 5_000, null),
                Manifest.Policies[1]
            ]
        };

        Assert.Throws<InvalidDataException>(tampered.ValidateFrozen);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<FrozenHoldoutPolicyDefinition>>(
            FrozenHoldoutPolicies.All);
    }

    [Fact]
    public void HoldoutEvaluatorContainsExactlyPoliciesAAndB()
    {
        Assert.Equal(2, FrozenHoldoutPolicies.All.Count);
        Assert.Equal(
            [
                FrozenHoldoutPolicyIds.Duration4Seconds,
                FrozenHoldoutPolicyIds.Duration17SecondsOrRegions2
            ],
            FrozenHoldoutPolicies.All.Select(x => x.PolicyId));
        Assert.Equal(2, Evaluate(Trial()).Count);
    }

    [Fact]
    public void HoldoutEvaluatorHasNoSweepOrProductionDependencies()
    {
        var referencedTypes = typeof(FrozenHoldoutEvaluator)
            .GetMethods()
            .SelectMany(method => method.GetParameters()
                .Select(x => x.ParameterType)
                .Append(method.ReturnType))
            .Select(x => x.FullName ?? x.Name)
            .ToArray();

        Assert.DoesNotContain(referencedTypes, value =>
            value.Contains("SimpleTemporalPolicySimulator", StringComparison.Ordinal) ||
            value.Contains("StepProgressApplier", StringComparison.Ordinal) ||
            value.Contains("DbContext", StringComparison.Ordinal) ||
            value.Contains("WalkamonContext", StringComparison.Ordinal));
    }

    [Fact]
    public void HoldoutResultCannotCarryRewardOrAuthoritativeEffects()
    {
        var names = typeof(HoldoutPolicyTrialResult)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(names, name =>
            name.Contains("Authoritative", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Reward", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Exp", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Pvp", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Recovered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReplacementKeepsOriginalPlannedSlot()
    {
        Assert.Equal(
            "ho-d1-slow-hand-02",
            HoldoutTrialMatrix.PlannedSlot("ho-d1-slow-hand-02r1"));
        Assert.Equal(28, HoldoutTrialMatrix.ExpectedTrialIds.Count);
    }

    private static IReadOnlyList<HoldoutPolicyTrialResult> Evaluate(HoldoutTrialRow trial) =>
        FrozenHoldoutPolicies.Evaluate(trial, Manifest.PolicyManifestHash);

    private static HoldoutTrialRow Trial(
        string trialId = "ho-d1-normal-hand-01",
        string scenario = "NORMAL_HAND",
        int groundTruth = 100,
        int counterDelta = 100,
        long maxDurationMs = 0,
        int regions = 0) => new(
        Manifest.PolicyManifestHash,
        trialId,
        HoldoutTrialMatrix.PlannedSlot(trialId),
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        "d1",
        "SM-A175F",
        "Samsung",
        "15",
        35,
        scenario,
        groundTruth,
        HoldoutTrialMatrix.PhonePosition(scenario),
        HoldoutTrialMatrix.WalkingSpeed(scenario),
        DateTime.UtcNow.AddMinutes(-2),
        DateTime.UtcNow,
        120,
        80,
        80,
        80,
        100,
        100 + counterDelta,
        counterDelta,
        60,
        50,
        5,
        5,
        regions,
        maxDurationMs,
        100_000,
        0,
        maxDurationMs,
        regions,
        0,
        HoldoutTechnicalValidity.Valid,
        null,
        "EVIDENCE");
}
