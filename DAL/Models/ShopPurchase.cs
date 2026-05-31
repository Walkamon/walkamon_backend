using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class ShopPurchase
{
    public long PurchaseId { get; set; }

    public Guid UserId { get; set; }

    public int ShopItemId { get; set; }

    public int Quantity { get; set; }

    public int ItemQuantitySnapshot { get; set; }

    public int UnitPriceAmount { get; set; }

    public DateTime PurchasedAt { get; set; }

    public virtual ShopItem ShopItem { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
