using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Friendship
{
    public Guid UserLowId { get; set; }

    public Guid UserHighId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User UserHigh { get; set; } = null!;

    public virtual User UserLow { get; set; } = null!;
}
