using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Item
{
    public int ItemId { get; set; }

    public string ItemName { get; set; } = null!;

    public string ItemTypeCode { get; set; } = null!;

    public string? EffectTypeCode { get; set; }

    public int? EffectValue { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

    public virtual ICollection<RewardPackageItem> RewardPackageItems { get; set; } = new List<RewardPackageItem>();

    public virtual ICollection<ShopItem> ShopItems { get; set; } = new List<ShopItem>();
}
