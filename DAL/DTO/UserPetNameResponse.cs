using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UserPetNameResponse
    {
        public Guid PetId { get; set; }

        public string PetName { get; set; } = null!;
    }
}
