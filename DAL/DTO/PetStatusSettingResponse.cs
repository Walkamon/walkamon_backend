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
            "Pet recovers this amount of Energy every 1 minute.";

        public string BondDescription { get; set; } =
            "Pet loses this amount of Bond every 1 minute.";

        public string LifeForceDescription { get; set; } =
            "Pet loses this amount of Life Force every 1 minute.";
    }
}
