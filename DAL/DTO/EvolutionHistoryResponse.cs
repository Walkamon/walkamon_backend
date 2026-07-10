using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class EvolutionHistoryResponse
    {
        public string PetName { get; set; } = null!;

        public string StageName { get; set; } = null!;

        public int StageNo { get; set; }

        public int Level { get; set; }

        public DateTime EvolvedAt { get; set; }
    }
}
