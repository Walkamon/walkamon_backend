using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.TransactionTests;

[Trait("UC", "UC-68")]
[Trait("UC", "UC-72")]
public sealed class PvpGameplayCalculatorTests
{
    [Theory]
    [InlineData(0, 10000)]
    [InlineData(1000, 11000)]
    public void CalculateSpeedBps_AppliesPassive(int passive, int expected)
    {
        Assert.Equal(expected, PvpGameplayCalculator.CalculateSpeedBps(passive, []));
    }

    [Fact]
    public void CalculateSpeedBps_AppliesBuffDebuffAndCaps()
    {
        Assert.Equal(11000, PvpGameplayCalculator.CalculateSpeedBps(1000, [("buff", 1500), ("debuff", 1500)]));
        Assert.Equal(12500, PvpGameplayCalculator.CalculateSpeedBps(1000, [("buff", 5000)]));
        Assert.Equal(7500, PvpGameplayCalculator.CalculateSpeedBps(0, [("debuff", 5000)]));
    }

    [Fact]
    public void CalculateDistanceUnits_UsesFixedPointMultiplier()
    {
        Assert.Equal(33000, PvpGameplayCalculator.CalculateDistanceUnits(3, 11000));
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(5000, 1750)]
    [InlineData(10000, 2500)]
    [InlineData(25000, 2500)]
    public void CalculateDailyPowerPaceMilli_UsesConfiguredCap(
        int dailySteps,
        int expectedPace)
    {
        Assert.Equal(
            expectedPace,
            PvpGameplayCalculator.CalculateDailyPowerPaceMilli(dailySteps));
    }

    [Fact]
    public void CalculatePacedDistanceUnits_UsesPaceDurationAndSpeedMultiplier()
    {
        Assert.Equal(
            825000,
            PvpGameplayCalculator.CalculatePacedDistanceUnits(
                TimeSpan.FromSeconds(30),
                2500,
                11000));
    }

    [Theory]
    [InlineData(0, 667, 200100)]
    [InlineData(5000, 1167, 350100)]
    [InlineData(10000, 1667, 500100)]
    public void CalculateDailyPowerPaceMilli_SproutStartsNearTwoHundredThousandAndStillScales(
        int dailySteps,
        int expectedPace,
        long expectedDistance)
    {
        var pace = PvpGameplayCalculator.CalculateDailyPowerPaceMilli(
            dailySteps,
            affinityCode: "sprout");

        Assert.Equal(expectedPace, pace);
        Assert.Equal(expectedDistance,
            PvpGameplayCalculator.CalculatePacedDistanceUnits(
                TimeSpan.FromSeconds(30),
                pace,
                PvpGameplayCalculator.BaseSpeedBps));
    }

    [Fact]
    public void CalculateDistanceUnits_SproutScalesLegacyStepDistanceByTwoThirds()
    {
        Assert.Equal(200000,
            PvpGameplayCalculator.CalculateDistanceUnits(30, 10000, "sprout"));
    }

    [Theory]
    [InlineData("2026-07-15T22:59:00Z", "moonlight", true)]   // 05:59 UTC+7
    [InlineData("2026-07-15T23:00:00Z", "dawn", true)]        // 06:00 UTC+7
    [InlineData("2026-07-16T04:59:00Z", "dawn", true)]        // 11:59 UTC+7
    [InlineData("2026-07-16T05:00:00Z", "warm_sun", true)]    // 12:00 UTC+7
    [InlineData("2026-07-16T10:59:00Z", "warm_sun", true)]    // 17:59 UTC+7
    [InlineData("2026-07-16T11:00:00Z", "moonlight", true)]   // 18:00 UTC+7
    [InlineData("2026-07-16T11:00:00Z", "warm_sun", false)]
    public void SpiritRules_UseVietnamBoundaries(string utcText, string affinity, bool expected)
    {
        var rules = Rules();
        Assert.Equal(expected, PvpGameplayCalculator.IsRuleActiveAtUtc(DateTime.Parse(utcText).ToUniversalTime(), rules.Single(x => x.AffinityCode == affinity)));
    }

    [Theory]
    [InlineData(-50, "mam_sang")]
    [InlineData(1000, "mam_sang")]
    [InlineData(1100, "choi_sang")]
    [InlineData(1300, "tan_sang")]
    [InlineData(1500, "linh_quang")]
    [InlineData(1700, "tinh_tu")]
    [InlineData(1900, "lumina")]
    [InlineData(5000, "lumina")]
    public void ResolveTier_UsesConfiguredBoundaries(int mmr, string expected)
    {
        Assert.Equal(expected, PvpGameplayCalculator.ResolveTier(mmr, Tiers()).TierCode);
    }

    private static List<PvpSpiritSpeedRule> Rules() =>
    [
        new() { AffinityCode = "dawn", StartMinute = 360, EndMinute = 719, BonusBps = 1000, IsActive = true },
        new() { AffinityCode = "warm_sun", StartMinute = 720, EndMinute = 1079, BonusBps = 1000, IsActive = true },
        new() { AffinityCode = "moonlight", StartMinute = 1080, EndMinute = 359, BonusBps = 1000, IsActive = true }
    ];

    private static List<PvpRankTier> Tiers() =>
    [
        new() { TierCode = "mam_sang", MinMmr = int.MinValue, IsActive = true },
        new() { TierCode = "choi_sang", MinMmr = 1100, IsActive = true },
        new() { TierCode = "tan_sang", MinMmr = 1300, IsActive = true },
        new() { TierCode = "linh_quang", MinMmr = 1500, IsActive = true },
        new() { TierCode = "tinh_tu", MinMmr = 1700, IsActive = true },
        new() { TierCode = "lumina", MinMmr = 1900, IsActive = true }
    ];
}
