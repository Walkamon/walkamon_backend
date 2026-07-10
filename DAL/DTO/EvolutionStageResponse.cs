using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class EvolutionStageResponse
    {
        public Guid StageId { get; set; }

        public int StageNo { get; set; }

        public string StageName { get; set; } = null!;

        public string? StateUrl { get; set; }

        public int RequiredLevel { get; set; }

        public bool IsCurrent { get; set; }

        public bool IsUnlocked { get; set; }

        public List<PetAnimationResponse> Animations { get; set; } = new();
    }
}
