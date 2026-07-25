using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UpdatePetRequest
    {
        public string PetName { get; set; } = null!;

        public int LifeForce { get; set; }

        public int Energy { get; set; }

        public int Bond { get; set; }

        public int Exp { get; set; }

        public double LifeForceRate { get; set; }

        public double EnergyRate { get; set; }

        public double BondRate { get; set; }

        public double ExpRate { get; set; }
    }
}
