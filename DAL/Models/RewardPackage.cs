using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class RewardPackage
{
    public Guid RewardPackageId { get; set; }

    public string PackageName { get; set; } = null!;

    public int WalletAmount { get; set; }

    public virtual ICollection<Achievement> Achievements { get; set; } = new List<Achievement>();

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    public virtual ICollection<RewardPackageItem> RewardPackageItems { get; set; } = new List<RewardPackageItem>();
}
