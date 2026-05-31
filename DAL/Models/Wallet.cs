using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Wallet
{
    public Guid UserId { get; set; }

    public int Balance { get; set; }

    public virtual User User { get; set; } = null!;
}
