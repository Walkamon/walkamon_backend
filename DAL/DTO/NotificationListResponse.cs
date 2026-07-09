namespace DAL.DTO;

public class NotificationListResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public List<NotificationListItemResponse> Notifications { get; set; } = [];
}
