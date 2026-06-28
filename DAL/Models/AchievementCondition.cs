using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class AchievementCondition
{
    public Guid AchievementConditionId { get; set; }

    public Guid AchievementId { get; set; }

    public string ConditionGroup { get; set; } = null!;

    public string ConditionCode { get; set; } = null!;

    public int TargetValue { get; set; }

    public Guid? ReferenceAchievementId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;

    public virtual Achievement? ReferenceAchievement { get; set; }
}
