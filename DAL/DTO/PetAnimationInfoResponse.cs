using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetAnimationInfoResponse
    {
        public string TypeAnimation { get; set; } = null!;

        public string? AnimationUrl { get; set; }
    }
}
