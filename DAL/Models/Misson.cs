using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Misson
{
    public int MissonId { get; set; }

    public string MissonTypeCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string MetricCode { get; set; } = null!;

    public int TargetValue { get; set; }

    public int RewardPackageId { get; set; }

    public bool IsCancelable { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public virtual RewardPackage RewardPackage { get; set; } = null!;

    public virtual ICollection<UserMisson> UserMissons { get; set; } = new List<UserMisson>();
}
