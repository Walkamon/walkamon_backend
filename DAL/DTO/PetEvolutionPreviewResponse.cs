using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetEvolutionPreviewResponse
    {
        public Guid PetId { get; set; }

        public string PetName { get; set; } = null!;

        public List<PetStageAnimationResponse> Stages { get; set; }
            = new();
    }
}
