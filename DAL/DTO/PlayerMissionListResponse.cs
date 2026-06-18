namespace DAL.DTO;

public class PlayerMissionListResponse
{
    public List<PlayerMissionItemResponse> DailyMissions { get; set; } = [];

    public List<PlayerMissionItemResponse> OverallMissions { get; set; } = [];
}
