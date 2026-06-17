namespace DAL.DTO;

public class AdminMissionListResponse
{
    public AdminMissionSummaryResponse Summary { get; set; } = new();

    public List<AdminMissionListItemResponse> Missions { get; set; } = [];
}
