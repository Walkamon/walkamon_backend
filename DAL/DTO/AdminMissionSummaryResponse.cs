namespace DAL.DTO;

public class AdminMissionSummaryResponse
{
    public int TotalMissions { get; set; }

    public int ActiveMissions { get; set; }

    public int OverallMissions { get; set; }

    public int WeeklyMissions { get; set; }

    public int MonthlyMissions { get; set; }

    public int TotalWalletAmount { get; set; }
}
