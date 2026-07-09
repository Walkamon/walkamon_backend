namespace DAL.DTO;

public class AdminNotificationListResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public List<AdminNotificationListItemResponse> Notifications { get; set; } = [];
}
