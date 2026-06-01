using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UserListResponse
    {
        public Guid UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? Username { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public string StatusCode { get; set; } = string.Empty;
    }
}
