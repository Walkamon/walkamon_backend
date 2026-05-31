using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetLevel
{
    public byte LevelNo { get; set; }

    public int MinLifeForce { get; set; }

    public virtual ICollection<PetStage> PetStages { get; set; } = new List<PetStage>();
}
