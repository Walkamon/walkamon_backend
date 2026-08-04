namespace DAL.DTO;

public class ClaimPlayerChallengeRewardResponse
{
    public Guid UserMissionId { get; set; }

    public Guid ChallengeId { get; set; }

    public string StatusCode { get; set; } = string.Empty;

    public int WalletAmount { get; set; }

    public int WalletBalance { get; set; }

    public List<PlayerChallengeRewardItemResponse> RewardItems { get; set; } = [];

    public DateTime ClaimedAt { get; set; }
}
