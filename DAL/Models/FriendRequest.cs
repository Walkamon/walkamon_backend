using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class FriendRequest
{
    public long RequestId { get; set; }

    public Guid SenderUserId { get; set; }

    public Guid ReceiverUserId { get; set; }

    public string StatusCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual User ReceiverUser { get; set; } = null!;

    public virtual User SenderUser { get; set; } = null!;
}
