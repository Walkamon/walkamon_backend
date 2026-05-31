using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetStage
{
    public int StageId { get; set; }

    public int SpeciesId { get; set; }

    public byte StageNo { get; set; }

    public string StageName { get; set; } = null!;

    public byte RequiredLevel { get; set; }

    public virtual ICollection<PetEvolutionHistory> PetEvolutionHistoryFromStages { get; set; } = new List<PetEvolutionHistory>();

    public virtual ICollection<PetEvolutionHistory> PetEvolutionHistoryToStages { get; set; } = new List<PetEvolutionHistory>();

    public virtual ICollection<Pet> Pets { get; set; } = new List<Pet>();

    public virtual PetLevel RequiredLevelNavigation { get; set; } = null!;

    public virtual PetSpecy Species { get; set; } = null!;
}
