namespace DAL.DTO;

public class DeviceTokenResponse
{
    public long DeviceTokenId { get; set; }

    public string FcmToken { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime UpdatedAt { get; set; }
}
