using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UserProfileResponse
    {
        public string? Username { get; set; }

        public string? Bio { get; set; }

        public string? Gender { get; set; }

        public DateOnly? Dob { get; set; }

        public string? AvatarUrl { get; set; }

        public bool HasSeenStory { get; set; }

        public string LanguageCode { get; set; } = string.Empty;

        public string ThemeCode { get; set; } = string.Empty;

        public string TimeZoneId { get; set; } = string.Empty;

        public bool ShowActivityStats { get; set; }

        public bool NotificationsEnabled { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
