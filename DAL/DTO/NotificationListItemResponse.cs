namespace DAL.DTO;

public class NotificationListItemResponse
{
    public Guid NotificationId { get; set; }

    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ShortBody { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public string? ContentCode { get; set; }
    public Dictionary<string, object?> Params { get; set; } = new();
}
