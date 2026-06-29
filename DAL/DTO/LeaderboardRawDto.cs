using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class LeaderboardRawDto
    {
        public Guid UserId { get; set; }

        public string? Username { get; set; }

        public string? Avatar { get; set; }

        public int StepCount { get; set; }
    }
}
