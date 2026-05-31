using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class DeviceToken
{
    public long DeviceTokenId { get; set; }

    public Guid UserId { get; set; }

    public string FcmToken { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
