using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class RewardPackageItem
{
    public Guid RewardPackageId { get; set; }

    public Guid ItemId { get; set; }

    public int Quantity { get; set; }

    public virtual Item Item { get; set; } = null!;

    public virtual RewardPackage RewardPackage { get; set; } = null!;
}
