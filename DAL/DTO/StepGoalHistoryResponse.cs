using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class StepGoalHistoryResponse
    {
        public DateOnly GoalDate { get; set; }

        public int TargetSteps { get; set; }

        public int CompletedSteps { get; set; }

        public bool IsCompleted { get; set; }
    }
}
