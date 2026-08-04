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

    public int NewUsersToday { get; set; }

    public int NewUsersThisMonth { get; set; }

    public double UserGrowthPercentage { get; set; }

    
    public long TotalSteps { get; set; }

    public long StepsToday { get; set; }

    public double AverageStepsPerDay { get; set; }

    public double AverageStepsPerUser { get; set; }

    public int WalkingUsersToday { get; set; }

    public double CompareWithYesterday { get; set; }

   
    public int TotalPets { get; set; }

    public double AveragePetLevel { get; set; }

    public int HighestPetLevel { get; set; }

   
    public List<PetInteractionStatisticDto> PetInteractions { get; set; } = new();
}
}
