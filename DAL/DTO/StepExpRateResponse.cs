using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class StepExpRateResponse
    {
        public string SettingKey { get; set; } = null!;

        public int BaseExp { get; set; }

        public string Description { get; set; } = null!;
    }
}
