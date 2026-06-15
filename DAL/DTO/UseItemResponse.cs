using System;

namespace DAL.DTO;

public class UseItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string EffectTypeCode { get; set; } = string.Empty;

    public int EffectValue { get; set; }

    public int RemainingQuantity { get; set; }

    public int LifeForce { get; set; }

    public int Energy { get; set; }

    public int Bond { get; set; }
}
