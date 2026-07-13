namespace BLL.Options;

public class DailyLoginRewardOptions
{
    public const string SectionName = "DailyLoginReward";

    public int[] Rewards { get; set; } = [10, 20, 30, 40, 50, 60, 100];
}
