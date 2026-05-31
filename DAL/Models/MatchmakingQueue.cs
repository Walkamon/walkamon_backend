using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class MatchmakingQueue
{
    public Guid UserId { get; set; }

    public string MatchTypeCode { get; set; } = null!;

    public string StatusCode { get; set; } = null!;

    public byte? PetLevelSnapshot { get; set; }

    public DateTime QueuedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
