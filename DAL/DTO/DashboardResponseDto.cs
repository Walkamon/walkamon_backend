using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class DashboardResponseDto
    {
        public int TotalUsers { get; set; }

        public int ActiveUsers { get; set; }

        public long TotalSteps { get; set; }

        public double UserGrowthPercentage { get; set; }

        public double AverageStepsPerDay { get; set; }

        public int WalkingUsersToday { get; set; }

        public double CompareWithYesterday { get; set; }

        public List<PetInteractionStatisticDto> PetInteractions { get; set; } = new();
    }
}
