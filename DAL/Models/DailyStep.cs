using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class DailyStep
{
    public Guid UserId { get; set; }

    public DateOnly StepDate { get; set; }

    public int StepCount { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
