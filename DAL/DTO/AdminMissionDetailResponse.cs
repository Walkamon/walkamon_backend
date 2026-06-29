namespace DAL.DTO;

public class AdminMissionDetailResponse
{
    public Guid MissionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string MissionTypeCode { get; set; } = string.Empty;

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int WalletAmount { get; set; }

    public List<AdminMissionRewardItemResponse> RewardItems { get; set; } = [];

    public List<AdminMissionConditionResponse> CompletionConditions { get; set; } = [];

    public List<AdminMissionConditionResponse> AssignmentConditions { get; set; } = [];
}
