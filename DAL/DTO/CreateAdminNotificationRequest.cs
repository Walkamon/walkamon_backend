using Microsoft.AspNetCore.Http;

namespace DAL.DTO;

public class CreateAdminNotificationRequest
{
    public string TypeCode { get; set; } = "server_announcement";

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string TargetAudienceCode { get; set; } = "all_users";

    public DateTime? ScheduleTime { get; set; }

    public bool SendNow { get; set; }

    public string? ImageUrl { get; set; }

    public IFormFile? Image { get; set; }
}
