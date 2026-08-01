using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PvpMatch
{
    public Guid MatchId { get; set; }

    public string MatchTypeCode { get; set; } = null!;

    public string SourceCode { get; set; } = null!;

    public string StatusCode { get; set; } = null!;

    public Guid? WinnerUserId { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime? CountdownEndsAt { get; set; }

    public DateTime? SettlementEndsAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int RatingK { get; set; }

    public int RatingDivisor { get; set; }

    public int SpeedMinBps { get; set; } = 7500;

    public int SpeedMaxBps { get; set; } = 12500;

    public byte ItemSlotLimit { get; set; } = 2;

    public int RuleVersion { get; set; } = 1;

    public long LastEventSequence { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<PvpMatchPlayer> PvpMatchPlayers { get; set; } = new List<PvpMatchPlayer>();

    public virtual ICollection<PvpMatchEvent> Events { get; set; } = new List<PvpMatchEvent>();

    public virtual ICollection<PvpMatchRewardEntitlement> RewardEntitlements { get; set; } = new List<PvpMatchRewardEntitlement>();

    public virtual ICollection<PvpMatchRewardSnapshot> RewardSnapshots { get; set; } = new List<PvpMatchRewardSnapshot>();

    public virtual ICollection<PvpMatchEffect> Effects { get; set; } = new List<PvpMatchEffect>();

    public virtual ICollection<PvpMatchLoadoutSlot> LoadoutSlots { get; set; } = new List<PvpMatchLoadoutSlot>();

    public virtual User? WinnerUser { get; set; }
}
