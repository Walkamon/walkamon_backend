using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserProfile
{
    public Guid UserId { get; set; }

    public string? Username { get; set; }

    public string? Bio { get; set; }

    public string? Gender { get; set; }

    public DateOnly? Dob { get; set; }

    public string? AvatarUrl { get; set; }

    public bool HasSeenStory { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string ThemeCode { get; set; } = null!;

    public string TimeZoneId { get; set; } = null!;

    public bool ShowActivityStats { get; set; }

    public bool NotificationsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
