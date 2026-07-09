using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class StreakHistoryResponse
    {
        public DateOnly StartDate { get; set; }

        public DateOnly EndDate { get; set; }

        public int Streak { get; set; }
    }
}
