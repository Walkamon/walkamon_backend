using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetInfoResponse
    {
        public Guid PetId { get; set; }

        public string PetName { get; set; } = string.Empty;

       

        public double ExpRate { get; set; }

        public double EnergyRate { get; set; }

        public double BondRate { get; set; }

        public double LifeForceRate { get; set; }
    }
}
