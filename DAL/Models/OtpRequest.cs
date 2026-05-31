using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class OtpRequest
{
    public long OtpRequestId { get; set; }

    public Guid UserId { get; set; }

    public string PurposeCode { get; set; } = null!;

    public string TargetValue { get; set; } = null!;

    public byte[] OtpHash { get; set; } = null!;

    public Guid RequestCode { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public short AttemptCount { get; set; }

    public short MaxAttempts { get; set; }

    public string StatusCode { get; set; } = null!;

    public string? RequestedIp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
