using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserFeedback
{
    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public string FeedbackTypeCode { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string StatusCode { get; set; } = null!;

    public string? AdminNote { get; set; }

    public Guid? HandledByUserId { get; set; }

    public DateTime? HandledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? HandledByUser { get; set; }

    public virtual User User { get; set; } = null!;
}
