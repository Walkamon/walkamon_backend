using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UpdatePetStatusSettingRequest
    {
        public int EnergyRecoverPerMinute { get; set; }

        public int BondDecreasePerMinute { get; set; }

        public int LifeForceDecreasePerMinute { get; set; }
    }
}
