namespace DAL.DTO;

public class AdminAchievementSummaryResponse
{
    public int TotalAchievements { get; set; }

    public int ActiveAchievements { get; set; }

    public int TotalUnlocks { get; set; }

    public double AverageCompletionRate { get; set; }
}
