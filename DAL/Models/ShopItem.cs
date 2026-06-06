using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class ShopItem
{
    public int ShopItemId { get; set; }

    public int ItemId { get; set; }

    public int ItemQuantity { get; set; }

    public int PriceAmount { get; set; }

    public bool IsActive { get; set; }

    public virtual Item Item { get; set; } = null!;

    public virtual ICollection<ShopPurchase> ShopPurchases { get; set; } = new List<ShopPurchase>();
}
