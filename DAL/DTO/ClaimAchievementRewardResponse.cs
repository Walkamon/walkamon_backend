namespace DAL.DTO;

public class ClaimAchievementRewardResponse
{
    public Guid AchievementId { get; set; }

    public int WalletAmount { get; set; }

    public List<PlayerAchievementRewardItemResponse> RewardItems { get; set; } = [];

    public int WalletBalance { get; set; }

    public DateTime ClaimedAt { get; set; }
}
