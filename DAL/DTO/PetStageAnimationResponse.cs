using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetStageAnimationResponse
    {
        public int StageNo { get; set; }

        public string StageName { get; set; } = null!;

        public string? StageImage { get; set; }

        public int RequiredLevel { get; set; }

        public List<PetAnimationInfoResponse> Animations { get; set; }
            = new();
    }
}
