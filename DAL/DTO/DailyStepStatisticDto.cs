using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class DailyStepStatisticDto
    {
        public string Type { get; set; } = string.Empty;

        public DateOnly FromDate { get; set; }

        public DateOnly ToDate { get; set; }

        public List<DailyStepStatisticItemDto> Data { get; set; } = new();
    }
}
