using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UserSummaryDto
    {
        public Guid UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? Username { get; set; }

        public string? AvatarUrl { get; set; }
    }
}
