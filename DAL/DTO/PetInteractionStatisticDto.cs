using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetInteractionStatisticDto
    {
        public string InteractionType { get; set; } = string.Empty;

        public int TotalCount { get; set; }

        public double Percentage { get; set; }
    }
}
