using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class EvolutionOptionResponse
    {
        public Guid PetId { get; set; }

        public string PetName { get; set; } = null!;

        public string? StateUrl { get; set; }

        public int RequiredLevel { get; set; }
    }
}
