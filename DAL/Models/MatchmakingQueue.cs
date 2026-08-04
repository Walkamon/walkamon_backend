using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class MatchmakingQueue
{
    public Guid UserId { get; set; }

    public string MatchTypeCode { get; set; } = null!;

    public string StatusCode { get; set; } = null!;

    public byte? PetLevelSnapshot { get; set; }

    public int? MmrSnapshot { get; set; }

    public int? DailyStepsSnapshot { get; set; }

    public int? BasePaceSnapshot { get; set; }

    public long? ExpectedDistanceUnits { get; set; }

    public int? ExpectedSpeedBps { get; set; }

    public int? PolicyVersion { get; set; }

    public bool RequiresRelief { get; set; }

    public DateTime? PowerSnapshotAt { get; set; }

    public DateTime? BotFallbackAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public DateTime QueuedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
