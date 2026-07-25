using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetListResponse
    {
        public Guid PetId { get; set; }

        public string PetName { get; set; } = null!;

        public int LifeForce { get; set; }

        public int Energy { get; set; }

        public int Bond { get; set; }

        public int Exp { get; set; }
    }
}
