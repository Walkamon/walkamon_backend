using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Achievement
{
    public Guid AchievementId { get; set; }

    public string Title { get; set; } = null!;

    public string CategoryCode { get; set; } = null!;

    public string MetricCode { get; set; } = null!;

    public int TargetValue { get; set; }

    public Guid RewardPackageId { get; set; }

    public bool IsActive { get; set; }

    public virtual RewardPackage RewardPackage { get; set; } = null!;

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
