using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class StepGoal
{
    public Guid UserId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public int TargetSteps { get; set; }

    public virtual User User { get; set; } = null!;
}
