namespace DAL.DTO;

public class DailyLoginRewardCalendarResponse
{
    public DateOnly ServerDate { get; set; }

    public bool CanClaimToday { get; set; }

    public DateOnly? LastClaimDate { get; set; }

    public int CurrentDay { get; set; }

    public List<DailyLoginRewardCalendarItemResponse> Rewards { get; set; } = [];
}
