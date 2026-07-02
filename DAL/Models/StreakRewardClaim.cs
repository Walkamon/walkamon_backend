using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Models
{
    public partial class StreakRewardClaim
    {
        public Guid UserId { get; set; }

        public DateOnly ClaimDate { get; set; }

        public int Streak { get; set; }

        public int Reward { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
