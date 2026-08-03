namespace BLL.Service;

public static class PvpBotRatingPolicy
{
    public static int CalculateDelta(string resultCode, int winDelta, int drawDelta, int lossDelta) => resultCode switch
    {
        "win" => winDelta,
        "draw" => drawDelta,
        "lose" => lossDelta,
        _ => throw new ArgumentOutOfRangeException(nameof(resultCode))
    };

    public static int ApplyPositiveRollingCap(int proposedDelta, int recentPositiveDelta, int positiveCap)
    {
        if (proposedDelta <= 0) return proposedDelta;
        return Math.Max(0, Math.Min(proposedDelta, positiveCap - Math.Max(0, recentPositiveDelta)));
    }
}
