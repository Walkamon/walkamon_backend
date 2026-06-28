namespace DAL.DTO;

public class AdminAchievementListResponse
{
    public AdminAchievementSummaryResponse Summary { get; set; } = new();

    public List<AdminAchievementListItemResponse> Achievements { get; set; } = [];
}
