using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class MissionCondition
{
    public Guid MissionConditionId { get; set; }

    public Guid MissionId { get; set; }

    public string ConditionGroup { get; set; } = null!;

    public string ConditionCode { get; set; } = null!;

    public int TargetValue { get; set; }

    public Guid? ReferenceMissionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Mission Mission { get; set; } = null!;

    public virtual Mission? ReferenceMission { get; set; }
}
