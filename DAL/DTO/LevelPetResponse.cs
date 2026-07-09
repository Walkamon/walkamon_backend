using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class LevelPetResponse
    {
        public int Level { get; set; }

        public int CurrentExp { get; set; }

        public int MaxExp { get; set; }
        public bool LevelUp { get; set; }
    }
}
