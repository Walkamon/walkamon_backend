using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Pet
{
    public Guid PetId { get; set; }

    public string PetName { get; set; } = null!;

    public string? PvpAffinityCode { get; set; }

    public double LifeForceRate { get; set; }

    public double EnergyRate { get; set; }

    public double BondRate { get; set; }

    // Legacy compatibility field; progression uses PetExpIncreasePerLevel.
    public double ExpRate { get; set; }

    public int LifeForce { get; set; }

    public int Energy { get; set; }

    public int Bond { get; set; }

    public int Exp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<PetStage> PetStages { get; set; } = new List<PetStage>();

    public virtual ICollection<PetAnimation> PetAnimations { get; set; } = new List<PetAnimation>();

    public virtual ICollection<UserPet> UserPets { get; set; } = new List<UserPet>();
}
