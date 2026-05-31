using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserMisson
{
    public long UserMissonId { get; set; }

    public Guid UserId { get; set; }

    public int MissonId { get; set; }

    public DateOnly CycleDate { get; set; }

    public DateTime AssignedAt { get; set; }

    public int ProgressValue { get; set; }

    public string StatusCode { get; set; } = null!;

    public DateTime? ClaimedAt { get; set; }

    public virtual Misson Misson { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
