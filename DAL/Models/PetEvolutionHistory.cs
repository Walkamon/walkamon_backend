using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetEvolutionHistory
{
    public Guid EvolutionId { get; set; }

    public Guid UserId { get; set; }

    public Guid FromStageId { get; set; }

    public Guid ToStageId { get; set; }

    public DateTime EvolvedAt { get; set; }

    public virtual PetStage FromStage { get; set; } = null!;

    public virtual PetStage ToStage { get; set; } = null!;

    public virtual Pet User { get; set; } = null!;
}
