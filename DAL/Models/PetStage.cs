using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetStage
{
    public Guid StageId { get; set; }

    public Guid PetId { get; set; }

    public string? StateUrl { get; set; }

    public int StageNo { get; set; }

    public string StageName { get; set; } = null!;

    public int RequiredLevel { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Pet Pet { get; set; } = null!;

    public virtual ICollection<PetEvolutionHistory> PetEvolutionHistories { get; set; } = new List<PetEvolutionHistory>();
}
