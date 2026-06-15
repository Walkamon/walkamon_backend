using System;

namespace DAL.DTO;

public class BuyShopItemResponse
{
    public Guid ShopItemId { get; set; }

    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int UnitPriceAmount { get; set; }

    public int TotalPriceAmount { get; set; }

    public int WalletBalance { get; set; }

    public int InventoryQuantity { get; set; }
}
