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

        public string? NormalizedUsername { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? Bio { get; set; }

        public string? AvatarUrl { get; set; }

        public string LanguageCode { get; set; } = string.Empty;

        public string ThemeCode { get; set; } = string.Empty;

        public string TimeZoneId { get; set; } = string.Empty;

        public string ProfileVisibilityCode { get; set; } = string.Empty;

        public bool ShowActivityStats { get; set; }

        public bool AllowFriendRequests { get; set; }

        public bool NotificationsEnabled { get; set; }

        public byte? QuietHourStart { get; set; }

        public byte? QuietHourEnd { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
