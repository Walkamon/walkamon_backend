using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class FriendSpiritResponse
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = null!;

        public string PetNickName { get; set; } = null!;

        public string PetName { get; set; } = null!;

        public int Level { get; set; }

        public int CurrentExp { get; set; }

        public int MaxExp { get; set; }

        public int CurrentEnergy { get; set; }

        public int MaxEnergy { get; set; }

        public int CurrentBond { get; set; }

        public int MaxBond { get; set; }

        public int CurrentLifeForce { get; set; }

        public int MaxLifeForce { get; set; }

        public string StageName { get; set; } = null!;

        public string? StageImage { get; set; }

        public List<PetAnimationResponse> Animations { get; set; } = new();
    }
}
