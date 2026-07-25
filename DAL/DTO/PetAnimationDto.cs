using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetAnimationDto
    {
        public Guid PetAnimationId { get; set; }

        public string TypeAnimation { get; set; } = null!;

        public int PetStageUse { get; set; }

        public string? AnimationUrl { get; set; }

        public bool IsActive { get; set; }
    }
}
