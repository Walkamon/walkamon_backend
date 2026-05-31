using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PvpMatch
{
    public Guid MatchId { get; set; }

    public string MatchTypeCode { get; set; } = null!;

    public string StatusCode { get; set; } = null!;

    public Guid? WinnerUserId { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual ICollection<PvpMatchPlayer> PvpMatchPlayers { get; set; } = new List<PvpMatchPlayer>();

    public virtual User? WinnerUser { get; set; }
}
