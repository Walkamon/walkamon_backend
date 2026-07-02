using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class ClaimRewardResponse
    {
        public int Streak { get; set; }

        public int Reward { get; set; }

        public int Balance { get; set; }

        public DateOnly ClaimDate { get; set; }
    }
}
