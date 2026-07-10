using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetLeaderboardResponse
    {
        public int Rank { get; set; }

        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string PetName { get; set; } = string.Empty;

        public int Level { get; set; }

        public int CurrentExp { get; set; }

        public int MaxExp { get; set; }

        public string StageName { get; set; } = string.Empty;

        public string? StageImage { get; set; }
    }
}
