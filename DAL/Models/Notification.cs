using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Notification
{
    public Guid NotificationId { get; set; }

    public string NotificationTypeCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string? ContentCode { get; set; }
    public string? ParamsJson { get; set; }
    public string? SourceLanguageCode { get; set; }
    public string? TitleVi { get; set; }
    public string? TitleEn { get; set; }
    public string? BodyVi { get; set; }
    public string? BodyEn { get; set; }
    public string? TranslationStatusCode { get; set; }
    public string? TranslationSourceHash { get; set; }
    public DateTime? TranslatedAt { get; set; }

    public string? ImageUrl { get; set; }

    public string TargetAudienceCode { get; set; } = null!;

    public string StatusCode { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public int RecipientCount { get; set; }

    public int DeliverySuccessCount { get; set; }

    public int DeliveryFailureCount { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
}
