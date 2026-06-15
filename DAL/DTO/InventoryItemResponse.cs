using System;

namespace DAL.DTO;

public class InventoryItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string ItemTypeName { get; set; } = string.Empty;

    public string? Image { get; set; }

    public string? EffectTypeCode { get; set; }

    public int? EffectValue { get; set; }

    public string? Description { get; set; }

    public int Quantity { get; set; }
}
