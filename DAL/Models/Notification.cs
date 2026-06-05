using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Notification
{
    public long NotificationId { get; set; }

    public string NotificationTypeCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public Guid? CreatedByUserId { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
}
