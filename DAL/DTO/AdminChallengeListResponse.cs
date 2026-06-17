namespace DAL.DTO;

public class AdminChallengeListResponse
{
    public AdminChallengeSummaryResponse Summary { get; set; } = new();

    public List<AdminChallengeListItemResponse> Challenges { get; set; } = [];
}
