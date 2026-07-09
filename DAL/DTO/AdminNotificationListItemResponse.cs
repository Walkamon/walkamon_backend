namespace DAL.DTO;

public class AdminNotificationListItemResponse
{
    public Guid NotificationId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string TargetAudienceCode { get; set; } = string.Empty;

    public string StatusCode { get; set; } = string.Empty;

    public DateTime? SendTime { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}
