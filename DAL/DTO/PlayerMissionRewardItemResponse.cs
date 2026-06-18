namespace DAL.DTO;

public class PlayerMissionRewardItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
