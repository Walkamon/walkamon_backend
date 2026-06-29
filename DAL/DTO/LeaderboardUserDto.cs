using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class LeaderboardUserDto
    {
        public int Rank { get; set; }

        public Guid UserId { get; set; }

        public string? Username { get; set; }

        public string? Avatar { get; set; }

        public int StepCount { get; set; }

        public bool IsCurrentUser { get; set; }
    }
}
