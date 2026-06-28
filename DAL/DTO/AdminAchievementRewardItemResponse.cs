namespace DAL.DTO;

public class AdminAchievementRewardItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
