using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetEvolutionHistory
{
    public Guid EvolutionId { get; set; }

    public Guid UserId { get; set; }

    public Guid StageId { get; set; }

    public int Level { get; set; }

    public DateTime EvolvedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual PetStage Stage { get; set; } = null!;
}
