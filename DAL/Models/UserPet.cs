using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserPet
{
    public Guid UserId { get; set; }

    public Guid PetId { get; set; }

    public int Level { get; set; }

    public string PetName { get; set; } = null!;

    public int PetExp { get; set; }
    public int PetEnergy { get; set; }
    public int PetBond { get; set; }
    public int PetLifeForce { get; set; }

   
    public int CurrentPetExp { get; set; }
    public int CurrentPetEnergy { get; set; }
    public int CurrentPetBond { get; set; }
    public int CurrentPetLifeForce { get; set; }

    
    public DateTime EnergyUpdatedAt { get; set; }
    public DateTime BondUpdatedAt { get; set; }
    public DateTime LifeForceUpdatedAt { get; set; }
    public DateTime ExpUpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Pet Pet { get; set; } = null!;
}
