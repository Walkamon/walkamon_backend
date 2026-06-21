namespace DAL.DTO;

public class ClaimMissionRewardResponse
{
    public Guid MissionId { get; set; }

    public Guid UserMissionId { get; set; }

    public int WalletAmount { get; set; }

    public List<PlayerMissionRewardItemResponse> RewardItems { get; set; } = [];

    public int WalletBalance { get; set; }

    public DateTime ClaimedAt { get; set; }
}
