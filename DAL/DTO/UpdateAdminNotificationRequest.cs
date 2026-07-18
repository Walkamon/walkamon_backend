using Microsoft.AspNetCore.Http;

namespace DAL.DTO;

public class UpdateAdminNotificationRequest
{
    public string? TypeCode { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? TargetAudienceCode { get; set; }

    public DateTime? ScheduleTime { get; set; }

    public string? ImageUrl { get; set; }

    public IFormFile? Image { get; set; }
}
