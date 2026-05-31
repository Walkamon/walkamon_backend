using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PvpMatchPlayer
{
    public Guid MatchId { get; set; }

    public Guid UserId { get; set; }

    public int StepsAtMatch { get; set; }

    public byte PetLevelAtMatch { get; set; }

    public int Score { get; set; }

    public bool IsReady { get; set; }

    public string? ResultCode { get; set; }

    public int? FinishTimeMs { get; set; }

    public DateTime JoinedAt { get; set; }

    public virtual PvpMatch Match { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
