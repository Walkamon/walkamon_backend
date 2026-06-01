using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UserDetailResponse
    {
        public Guid UserId { get; set; }

        public int RoleId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string NormalizedEmail { get; set; } = string.Empty;

        public bool EmailConfirmed { get; set; }

        public string StatusCode { get; set; } = string.Empty;

        public int AccessFailedCount { get; set; }

        public DateTime? LockoutEndAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime? PasswordChangedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public UserProfileResponse? Profile { get; set; }
    }
}
