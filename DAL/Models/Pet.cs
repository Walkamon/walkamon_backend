using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Pet
{
    public Guid UserId { get; set; }

    public int CurrentStageId { get; set; }

    public string PetName { get; set; } = null!;

    public int LifeForce { get; set; }

    public int Energy { get; set; }

    public int Bond { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual PetStage CurrentStage { get; set; } = null!;

    public virtual ICollection<PetEvolutionHistory> PetEvolutionHistories { get; set; } = new List<PetEvolutionHistory>();

    public virtual User User { get; set; } = null!;
}
