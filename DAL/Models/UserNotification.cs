using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserNotification
{
    public Guid UserId { get; set; }

    public long NotificationId { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Notification Notification { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
