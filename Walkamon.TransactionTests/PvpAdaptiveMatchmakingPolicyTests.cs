using BLL.Exceptions;
using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.TransactionTests;

[Trait("Feature", "PvpAdaptiveMatchmaking")]
public sealed class PvpAdaptiveMatchmakingPolicyTests
{
    [Theory]
    [InlineData(0, 1000)]
    [InlineData(5000, 1750)]
    [InlineData(10000, 2500)]
    [InlineData(50000, 2500)]
    public void MatchPower_UsesValidatedDailyPowerAndClampsPace(int dailySteps, int expectedPace)
    {
        var snapshot = PvpMatchPowerCalculator.Calculate(
            dailySteps,
            10000,
            1000,
            2500,
            0,
            [],
            false,
            PvpMatchPowerCalculator.HumanExpectedItemUseRateBps,
            30,
            7500,
            12500,
            DateTime.UnixEpoch);

        Assert.Equal(expectedPace, snapshot.BasePaceMilliStepsPerSecond);
        Assert.Equal(10000, snapshot.ExpectedSpeedBps);
        Assert.Equal(expectedPace * 300L, snapshot.ExpectedDistanceUnits);
    }

    [Fact]
    public void MatchPower_ValuesOnlyDistanceChangingLoadoutAndRespectsSpeedCap()
    {
        var snapshot = PvpMatchPowerCalculator.Calculate(
            10000,
            10000,
            1000,
            2500,
            1000,
            [
                new("pvp_speed_up", 1500, 5000),
                new("pvp_speed_down", 1500, 5000),
                new("pvp_shield", 5000, 8000),
                new("pvp_cleanse", 5000, 0)
            ],
            true,
            10000,
            30,
            7500,
            12500,
            DateTime.UnixEpoch);

        Assert.Equal(500, snapshot.ExpectedLoadoutBps);
        Assert.Equal(11500, snapshot.ExpectedSpeedBps);
        Assert.Equal(862500, snapshot.ExpectedDistanceUnits);
    }

    [Fact]
    public void MatchQuality_ExpandsSoftWindowButNeverCrossesHardLimit()
    {
        var policy = PvpMatchmakingPolicyProvider.CreateDefault();
        var first = Power(1000, 300000);
        var softOpponent = Power(1120, 315000);
        var hardOpponent = Power(1000, 230000);

        Assert.False(PvpMatchQualityEvaluator.Evaluate(
            1000, first, TimeSpan.Zero,
            1120, softOpponent, TimeSpan.Zero,
            policy).IsEligible);
        Assert.True(PvpMatchQualityEvaluator.Evaluate(
            1000, first, TimeSpan.FromSeconds(11),
            1120, softOpponent, TimeSpan.FromSeconds(11),
            policy).IsEligible);
        Assert.Equal("hard_power_gap", PvpMatchQualityEvaluator.Evaluate(
            1000, first, TimeSpan.FromMinutes(5),
            1000, hardOpponent, TimeSpan.FromMinutes(5),
            policy).ReasonCode);
    }

    [Theory]
    [InlineData(0, 1000, "easy")]
    [InlineData(0, 3000, "fair")]
    [InlineData(0, 9000, "hard")]
    [InlineData(2, 1000, "easy")]
    [InlineData(2, 5000, "fair")]
    [InlineData(2, 9500, "hard")]
    [InlineData(4, 6000, "easy")]
    [InlineData(4, 9000, "fair")]
    [InlineData(5, 9999, "relief")]
    public void BotSelector_UsesConfiguredLossStreakWeights(int streak, int roll, string expected)
    {
        var policy = PvpMatchmakingPolicyProvider.CreateDefault();
        var selected = PvpBotDifficultySelector.Select(streak, null, 0, 0, roll, policy);

        Assert.NotNull(selected);
        Assert.Equal(expected, selected.Value.DifficultyCode);
    }

    [Fact]
    public void BotSelector_BlocksExposureAndConsecutiveHardButReliefOverridesExposure()
    {
        var policy = PvpMatchmakingPolicyProvider.CreateDefault();

        Assert.Null(PvpBotDifficultySelector.Select(0, null, 0, 6, 1000, policy));
        Assert.NotEqual("hard", PvpBotDifficultySelector.Select(0, "hard", 1, 0, 9999, policy)!.Value.DifficultyCode);
        Assert.Equal("relief", PvpBotDifficultySelector.Select(5, "hard", 1, 10, 9999, policy)!.Value.DifficultyCode);
    }

    [Theory]
    [InlineData("easy", 1000, 8200, true)]
    [InlineData("easy", 9500, 8200, false)]
    [InlineData("hard", 1000, 3000, true)]
    [InlineData("hard", 9000, 3000, false)]
    public void BotTier_IsProbabilisticRatherThanScripted(
        string difficulty,
        int roll,
        int targetUserWin,
        bool botExpectedBehind)
    {
        var decision = new PvpBotDifficultyDecision(
            difficulty,
            false,
            roll,
            targetUserWin,
            "test");
        var ratio = PvpBotDifficultySelector.GetTargetBotDistanceRatioBps(decision);

        Assert.Equal(botExpectedBehind, ratio < 10000);
    }

    [Fact]
    public void Calibration_SnapshotsTargetDistanceWithinPaceAndPowerHardLimits()
    {
        var user = Power(2000, 600000);
        var calibration = new PvpBotCalibrationService().Calibrate(
            user,
            10000,
            1000,
            2500,
            30,
            2000,
            9700);

        Assert.NotNull(calibration);
        Assert.InRange(calibration.Value.CalibratedPaceMilli, 1000, 2500);
        Assert.InRange(calibration.Value.ExpectedGapBps, 0, 2000);
        Assert.InRange(calibration.Value.ExpectedDistanceUnits, 570000, 600000);
    }

    [Theory]
    [InlineData(false, true, true, "lose", 4, 4, "not_normal_completion")]
    [InlineData(true, false, true, "lose", 4, 4, "not_ready")]
    [InlineData(true, true, false, "lose", 4, 4, "realtime_not_joined")]
    [InlineData(true, true, true, "lose", 4, 5, "valid_loss")]
    [InlineData(true, true, true, "win", 4, 0, "valid_win")]
    [InlineData(true, true, true, "draw", 4, 0, "valid_draw")]
    public void LossStreak_CountsOnlyValidCompletedRankedEvidence(
        bool normal,
        bool ready,
        bool joined,
        string result,
        int current,
        int expected,
        string code)
    {
        var decision = PvpLossStreakPolicy.Evaluate(
            current,
            new(true, false, normal, ready, joined, result));

        Assert.Equal(expected, decision.NewLossStreak);
        Assert.Equal(code, decision.EligibilityCode);
    }

    [Fact]
    public void ReliefCompletion_ResetsStreakRegardlessOfResult()
    {
        var decision = PvpLossStreakPolicy.Evaluate(
            5,
            new(true, true, true, true, true, "lose"));

        Assert.True(decision.IsEligible);
        Assert.True(decision.ResetByRelief);
        Assert.Equal(0, decision.NewLossStreak);
    }

    [Theory]
    [InlineData(6, 0, 8, 6)]
    [InlineData(6, 5, 8, 3)]
    [InlineData(6, 9, 8, 0)]
    [InlineData(-2, 100, 8, -2)]
    public void BotRating_PositiveRollingCapPreventsInfiniteMmr(
        int proposed,
        int recentPositive,
        int cap,
        int expected) =>
        Assert.Equal(expected, PvpBotRatingPolicy.ApplyPositiveRollingCap(proposed, recentPositive, cap));

    [Fact]
    public void PolicyProviderValidation_RejectsInvalidWeightsAndWindows()
    {
        var invalidWeights = PvpMatchmakingPolicyProvider.CreateDefault();
        invalidWeights.Streak01HardWeightBps = 2999;
        Assert.Throws<ConflictException>(() => PvpMatchmakingPolicyProvider.Validate(invalidWeights));

        var invalidWindows = PvpMatchmakingPolicyProvider.CreateDefault();
        invalidWindows.Stage1MmrGap = 200;
        Assert.Throws<ConflictException>(() => PvpMatchmakingPolicyProvider.Validate(invalidWindows));
    }

    private static PvpPowerSnapshot Power(int pace, long distance) =>
        new(0, pace, 0, 0, 10000, distance, DateTime.UnixEpoch);
}
