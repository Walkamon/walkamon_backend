using Microsoft.AspNetCore.Http;

namespace DAL.DTO;

public class CreateAdminAchievementRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IFormFile? Icon { get; set; }

    public bool IsActive { get; set; } = true;

    public int WalletAmount { get; set; }

    public string MetricCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public List<AdminAchievementRewardItemRequest> RewardItems { get; set; } = [];

    public List<AdminAchievementConditionRequest> AssignmentConditions { get; set; } = [];
}
