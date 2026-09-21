using System;

namespace DAL.DTO;

public class BuyShopItemRequest
{
    // Reuse this ID when retrying the same purchase; omit only for legacy clients.
    public Guid? RequestId { get; set; }

    public Guid ShopItemId { get; set; }

    public int Quantity { get; set; }
}
