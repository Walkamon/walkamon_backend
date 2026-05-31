using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class ExternalLogin
{
    public long ExternalLoginId { get; set; }

    public Guid UserId { get; set; }

    public string ProviderName { get; set; } = null!;

    public string ProviderSubject { get; set; } = null!;

    public string? ProviderEmail { get; set; }

    public string? ProviderDisplayName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public virtual User User { get; set; } = null!;
}
