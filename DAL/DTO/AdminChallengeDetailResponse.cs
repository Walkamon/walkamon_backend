namespace DAL.DTO;

public class AdminChallengeDetailResponse
{
    public Guid ChallengeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ChallengeTypeCode { get; set; } = string.Empty;

    public string ChallengeTypeName { get; set; } = string.Empty;

    public string MetricCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int Participants { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool IsCancelable { get; set; }

    public int WalletAmount { get; set; }

    public List<AdminChallengeRewardItemResponse> RewardItems { get; set; } = [];
}
