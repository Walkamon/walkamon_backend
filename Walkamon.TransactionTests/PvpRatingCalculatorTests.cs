using BLL.Service;
using Xunit;

namespace Walkamon.TransactionTests;

public class PvpRatingCalculatorTests
{
    [Theory]
    [InlineData(1000, 1000, "win", 16)]
    [InlineData(1000, 1000, "draw", 0)]
    [InlineData(1200, 1000, "win", 8)]
    [InlineData(1200, 1000, "lose", -24)]
    [InlineData(1200, 1000, "draw", -8)]
    public void CalculateDelta_UsesConfiguredEloFormula(int playerMmr, int opponentMmr, string result, int expected)
    {
        Assert.Equal(expected, PvpRatingCalculator.CalculateDelta(playerMmr, opponentMmr, result));
    }

    [Fact]
    public void CalculateDelta_IsZeroSumForHumanMatch()
    {
        var winner = PvpRatingCalculator.CalculateDelta(1200, 1000, "win");
        var loser = PvpRatingCalculator.CalculateDelta(1000, 1200, "lose");

        Assert.Equal(-winner, loser);
    }
}
