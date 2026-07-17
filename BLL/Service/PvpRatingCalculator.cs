namespace BLL.Service;

public static class PvpRatingCalculator
{
    public static int CalculateDelta(int playerRating, int opponentRating, string resultCode, int ratingK = 32, int ratingDivisor = 400)
    {
        var actual = resultCode switch
        {
            "win" => 1d,
            "draw" => .5d,
            "lose" => 0d,
            _ => throw new ArgumentOutOfRangeException(nameof(resultCode), "Result must be win, draw or lose.")
        };
        var expected = 1d / (1d + Math.Pow(10d, (opponentRating - playerRating) / (double)ratingDivisor));
        return (int)Math.Round(ratingK * (actual - expected), MidpointRounding.AwayFromZero);
    }
}
