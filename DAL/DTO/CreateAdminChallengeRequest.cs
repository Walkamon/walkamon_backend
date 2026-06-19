namespace DAL.DTO;

public class CreateAdminChallengeRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ChallengeTypeCode { get; set; } = string.Empty;

    public string MetricCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public bool IsCancelable { get; set; }

    public bool IsActive { get; set; } = true;

    public int WalletAmount { get; set; }

    public List<AdminChallengeRewardItemRequest> RewardItems { get; set; } = [];
}
