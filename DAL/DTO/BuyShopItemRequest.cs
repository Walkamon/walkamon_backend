using System;

namespace DAL.DTO;

public class BuyShopItemRequest
{
    public Guid ShopItemId { get; set; }

    public int Quantity { get; set; }
}
