namespace DAL.DTO;

public class PlayerChallengeRewardItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
