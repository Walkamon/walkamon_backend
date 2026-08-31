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

    public bool IsActive { get; set; }

    public string UsageContextCode { get; set; } = "none";

    public bool CanUseNow { get; set; }

    public bool CanEquipForPvp { get; set; }
    public string? ItemNameVi { get; set; }
    public string? ItemNameEn { get; set; }
    public string? DescriptionVi { get; set; }
    public string? DescriptionEn { get; set; }
}
