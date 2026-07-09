using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Models
{
    public partial class PetInteraction
    {
        public Guid InteractionId { get; set; }

        public Guid UserId { get; set; }

        public string InteractionType { get; set; } = null!;

        public DateOnly InteractionDate { get; set; }

        public int Count { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
