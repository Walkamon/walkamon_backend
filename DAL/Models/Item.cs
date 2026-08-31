using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Item
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = null!;

    public string? ImgUrl { get; set; }

    public Guid ItemTypeId { get; set; }

    public string? EffectTypeCode { get; set; }

    public int? EffectValue { get; set; }

    public string? Description { get; set; }

    // Localized content is additive; ItemName/Description remain the legacy
    // source fields for older clients and admin forms.
    public string? ContentCode { get; set; }
    public string? SourceLanguageCode { get; set; }
    public string? ItemNameVi { get; set; }
    public string? ItemNameEn { get; set; }
    public string? DescriptionVi { get; set; }
    public string? DescriptionEn { get; set; }
    public string? TranslationStatusCode { get; set; }
    public string? TranslationSourceHash { get; set; }
    public DateTime? TranslatedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

    public virtual ItemType ItemType { get; set; } = null!;

    public virtual ICollection<RewardPackageItem> RewardPackageItems { get; set; } = new List<RewardPackageItem>();

    public virtual ShopItem? ShopItem { get; set; }
}
