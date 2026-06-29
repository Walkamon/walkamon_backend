using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class DailyStepStatisticItemDto
    {
        public string Label { get; set; } = string.Empty;

        public int StepCount { get; set; }
    }
}
