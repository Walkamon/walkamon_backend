using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class InventoryItem
{
    public Guid UserId { get; set; }

    public Guid ItemId { get; set; }

    public int Quantity { get; set; }

    public virtual Item Item { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
