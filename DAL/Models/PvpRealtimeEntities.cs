namespace DAL.Models;

public sealed class PvpItemEffectDefinition
{
    public Guid PvpItemEffectDefinitionId { get; set; }
    public Guid ItemId { get; set; }
    public string EffectCode { get; set; } = null!;
    public string TargetCode { get; set; } = null!;
    public int MagnitudeBps { get; set; }
    public int DurationMs { get; set; }
    public int CooldownMs { get; set; }
    public string AssetKey { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Item Item { get; set; } = null!;
}

public sealed class PvpPlayerLoadoutSlot
{
    public Guid UserId { get; set; }
    public byte SlotNo { get; set; }
    public Guid ItemId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public sealed class PvpBotLoadoutSlot
{
    public Guid BotProfileId { get; set; }
    public byte SlotNo { get; set; }
    public Guid ItemId { get; set; }
    public PvpBotProfile BotProfile { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public sealed class PvpMatchLoadoutSlot
{
    public Guid PvpMatchLoadoutSlotId { get; set; }
    public Guid MatchId { get; set; }
    public Guid MatchPlayerId { get; set; }
    public byte SlotNo { get; set; }
    public Guid ItemId { get; set; }
    public string EffectCode { get; set; } = null!;
    public string TargetCode { get; set; } = null!;
    public int MagnitudeBps { get; set; }
    public int DurationMs { get; set; }
    public int CooldownMs { get; set; }
    public string AssetKey { get; set; } = null!;
    public DateTime? UsedAt { get; set; }
    public PvpMatch Match { get; set; } = null!;
    public PvpMatchPlayer MatchPlayer { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public sealed class PvpMatchItemAction
{
    public Guid PvpMatchItemActionId { get; set; }
    public Guid MatchId { get; set; }
    public Guid ActorMatchPlayerId { get; set; }
    public Guid? TargetMatchPlayerId { get; set; }
    public Guid MatchLoadoutSlotId { get; set; }
    public Guid ClientActionId { get; set; }
    public string ResultCode { get; set; } = null!;
    public string EffectCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public PvpMatch Match { get; set; } = null!;
    public PvpMatchPlayer Actor { get; set; } = null!;
    public PvpMatchPlayer? Target { get; set; }
    public PvpMatchLoadoutSlot MatchLoadoutSlot { get; set; } = null!;
}

public sealed class PvpMatchEffect
{
    public Guid PvpMatchEffectId { get; set; }
    public Guid MatchId { get; set; }
    public Guid TargetMatchPlayerId { get; set; }
    public Guid? SourceMatchPlayerId { get; set; }
    public Guid? SourceItemActionId { get; set; }
    public string EffectCode { get; set; } = null!;
    public string EffectKindCode { get; set; } = null!;
    public int MagnitudeBps { get; set; }
    public string StatusCode { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public PvpMatch Match { get; set; } = null!;
    public PvpMatchPlayer Target { get; set; } = null!;
}

public sealed class PvpSpiritSpeedRule
{
    public string AffinityCode { get; set; } = null!;
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public int BonusBps { get; set; }
    public string TimeZoneCode { get; set; } = "Asia/Ho_Chi_Minh";
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PvpRankTier
{
    public string TierCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public int MinMmr { get; set; }
    public short SortOrder { get; set; }
    public string AssetKey { get; set; } = null!;
    public string ColorHex { get; set; } = null!;
    public bool IsActive { get; set; }
}
