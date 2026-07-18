using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class CurrentAnimationResponse
    {
        public string AnimationType { get; set; } = null!;

        public string AnimationUrl { get; set; } = null!;

        public int StageNo { get; set; }

        public string StageName { get; set; } = null!;
    }
}
