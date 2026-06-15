using System;

namespace DAL.DTO;

public class ShopItemDetailResponse
{
    public Guid ShopItemId { get; set; }

    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string ItemTypeName { get; set; } = string.Empty;

    public string? Image { get; set; }

    public int PriceAmount { get; set; }

    public string? EffectTypeCode { get; set; }

    public int? EffectValue { get; set; }

    public string? Description { get; set; }
}
