using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetStageDto
    {
        public Guid StageId { get; set; }

        public int StageNo { get; set; }

        public string StageName { get; set; } = null!;

        public int RequiredLevel { get; set; }

        public string? StateUrl { get; set; }

        public bool IsActive { get; set; }
    }
}
