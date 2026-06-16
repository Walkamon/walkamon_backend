using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Mission
{
    public Guid MissionId { get; set; }

    public string MissionTypeCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string MetricCode { get; set; } = null!;

    public int TargetValue { get; set; }

    public Guid RewardPackageId { get; set; }

    public bool IsCancelable { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public string? Description { get; set; }

    public virtual RewardPackage RewardPackage { get; set; } = null!;

    public virtual ICollection<UserMission> UserMissions { get; set; } = new List<UserMission>();
}
