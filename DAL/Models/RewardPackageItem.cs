using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class RewardPackageItem
{
    public int RewardPackageId { get; set; }

    public int ItemId { get; set; }

    public int Quantity { get; set; }

    public virtual Item Item { get; set; } = null!;

    public virtual RewardPackage RewardPackage { get; set; } = null!;
}
