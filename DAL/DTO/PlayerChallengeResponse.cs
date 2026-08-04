namespace DAL.DTO;

public class PlayerChallengeResponse
{
    public Guid UserMissionId { get; set; }

    public Guid ChallengeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string MetricCode { get; set; } = string.Empty;

    public int ProgressValue { get; set; }

    public int TargetValue { get; set; }

    public int WalletAmount { get; set; }

    public List<PlayerChallengeRewardItemResponse> RewardItems { get; set; } = [];

    public bool IsCancelable { get; set; }

    public string StatusCode { get; set; } = string.Empty;

    public bool CanClaim { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? ClaimedAt { get; set; }
}
