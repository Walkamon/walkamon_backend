using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class PetStatusSettingResponse
    {
        public int EnergyRecoverPerMinute { get; set; }

        public int BondDecreasePerMinute { get; set; }

        public int LifeForceDecreasePerMinute { get; set; }

        public string EnergyDescription { get; set; } =
            "Legacy field: interval in minutes required to recover 1 Energy.";

        public string BondDescription { get; set; } =
            "Legacy field: percentage of Max Bond lost every 24 hours.";

        public string LifeForceDescription { get; set; } =
            "Legacy field: percentage of Max Life Force lost every 24 hours.";
    }
}
