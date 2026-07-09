using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetStatusResponse
    {
        

        public int CurrentEnergy { get; set; }

        public int MaxEnergy { get; set; }

        public int CurrentBond { get; set; }

        public int MaxBond { get; set; }

        public int CurrentLifeForce { get; set; }

        public int MaxLifeForce { get; set; }

       
    }
}
