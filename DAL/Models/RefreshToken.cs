using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class RefreshToken
{
    public long RefreshTokenId { get; set; }

    public Guid UserId { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public Guid? JwtId { get; set; }

    public string? DeviceId { get; set; }

    public string? DeviceName { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public long? ReplacedByTokenId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<RefreshToken> InverseReplacedByToken { get; set; } = new List<RefreshToken>();

    public virtual RefreshToken? ReplacedByToken { get; set; }

    public virtual User User { get; set; } = null!;
}
