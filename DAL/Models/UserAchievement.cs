using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserAchievement
{
    public Guid UserId { get; set; }

    public int AchievementId { get; set; }

    public int ProgressValue { get; set; }

    public DateTime? UnlockedAt { get; set; }

    public DateTime? ClaimedAt { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
