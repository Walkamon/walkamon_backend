using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Shop
{
    public int ShopId { get; set; }

    public string ShopName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<ShopItem> ShopItems { get; set; } = new List<ShopItem>();
}
