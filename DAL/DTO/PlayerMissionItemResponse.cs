namespace DAL.DTO;

public class PlayerMissionItemResponse
{
    public Guid MissionId { get; set; }

    public Guid? UserMissionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string MissionTypeCode { get; set; } = string.Empty;

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public string MetricCode { get; set; } = string.Empty;

    public int ProgressValue { get; set; }

    public int TargetValue { get; set; }

    public int WalletAmount { get; set; }

    public List<PlayerMissionRewardItemResponse> RewardItems { get; set; } = [];

    public string StatusCode { get; set; } = string.Empty;

    public bool CanClaim { get; set; }

    public DateTime? ClaimedAt { get; set; }
}
