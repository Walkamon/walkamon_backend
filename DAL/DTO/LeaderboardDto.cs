using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class LeaderboardDto
    {
        public string Type { get; set; } = string.Empty;

        public DateOnly FromDate { get; set; }

        public DateOnly ToDate { get; set; }

        public int MyRank { get; set; }

        public List<LeaderboardUserDto> Leaderboard { get; set; } = new();
    }
}
