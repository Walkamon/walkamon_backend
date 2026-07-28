using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PvpMatchPlayer
{
    public Guid MatchPlayerId { get; set; }

    public Guid MatchId { get; set; }

    public Guid? UserId { get; set; }

    public Guid? BotProfileId { get; set; }

    public string ParticipantTypeCode { get; set; } = null!;

    public int StepsAtMatch { get; set; }

    public byte PetLevelAtMatch { get; set; }

    public int Score { get; set; }

    public int MmrBefore { get; set; }

    public int MmrDelta { get; set; }

    public Guid? PetIdSnapshot { get; set; }

    public string? PetNameSnapshot { get; set; }

    public byte? PetStageNoSnapshot { get; set; }

    public string? SpiritAffinityCode { get; set; }

    public int PassiveSpeedBps { get; set; }

    public int ValidatedSteps { get; set; }

    public int DailyEligibleStepsSnapshot { get; set; }

    public int BasePaceMilliStepsPerSecond { get; set; } = 1000;

    public long DistanceUnits { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public bool IsReady { get; set; }

    public string? ResultCode { get; set; }

    public int? FinishTimeMs { get; set; }

    public DateTime JoinedAt { get; set; }

    public virtual PvpMatch Match { get; set; } = null!;

    public virtual User? User { get; set; }

    public virtual PvpBotProfile? BotProfile { get; set; }

    public virtual ICollection<PvpMatchEffect> EffectsReceived { get; set; } = new List<PvpMatchEffect>();

    public virtual ICollection<PvpMatchLoadoutSlot> LoadoutSlots { get; set; } = new List<PvpMatchLoadoutSlot>();
}
