namespace DAL.DTO;

public class AdminNotificationDetailResponse
{
    public Guid NotificationId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string TargetAudienceCode { get; set; } = string.Empty;

    public DateTime? ScheduleTime { get; set; }

    public DateTime? SentAt { get; set; }

    public int RecipientCount { get; set; }

    public string StatusCode { get; set; } = string.Empty;

    public int DeliverySuccessCount { get; set; }

    public int DeliveryFailureCount { get; set; }

    public string? ImageUrl { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
