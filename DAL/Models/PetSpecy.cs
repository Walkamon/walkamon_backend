using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetSpecy
{
    public int SpeciesId { get; set; }

    public string SpeciesName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<PetStage> PetStages { get; set; } = new List<PetStage>();
}
