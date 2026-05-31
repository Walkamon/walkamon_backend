using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetEvolutionHistory
{
    public long EvolutionId { get; set; }

    public Guid UserId { get; set; }

    public int FromStageId { get; set; }

    public int ToStageId { get; set; }

    public DateTime EvolvedAt { get; set; }

    public virtual PetStage FromStage { get; set; } = null!;

    public virtual PetStage ToStage { get; set; } = null!;

    public virtual Pet User { get; set; } = null!;
}
