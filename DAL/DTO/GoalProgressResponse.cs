using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class GoalProgressResponse
    {
        public int TargetSteps { get; set; }

        public int CurrentSteps { get; set; }

        public int RemainingSteps { get; set; }

        public double ProgressPercent { get; set; }

        public bool Completed { get; set; }
    }
}
