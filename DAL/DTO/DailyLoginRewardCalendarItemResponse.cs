namespace DAL.DTO;

public class DailyLoginRewardCalendarItemResponse
{
    public int Day { get; set; }

    public int Reward { get; set; }

    public string Status { get; set; } = string.Empty;
}
