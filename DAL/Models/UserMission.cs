using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserMission
{
    public Guid UserMissionId { get; set; }

    public Guid UserId { get; set; }

    public Guid MissionId { get; set; }

    public DateOnly CycleDate { get; set; }

    public DateTime AssignedAt { get; set; }

    public int ProgressValue { get; set; }

    public string StatusCode { get; set; } = null!;

    public DateTime? ClaimedAt { get; set; }

    public virtual Mission Mission { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
