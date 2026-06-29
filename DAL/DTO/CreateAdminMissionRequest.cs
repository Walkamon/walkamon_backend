namespace DAL.DTO;

public class CreateAdminMissionRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MissionTypeCode { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public bool IsActive { get; set; } = true;

    public int WalletAmount { get; set; }

    public List<AdminMissionRewardItemRequest> RewardItems { get; set; } = [];

    public List<AdminMissionConditionRequest> CompletionConditions { get; set; } = [];

    public List<AdminMissionConditionRequest> AssignmentConditions { get; set; } = [];
}
