using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class FriendRequestResponse
    {
        public Guid RequestId { get; set; }

        public Guid SenderUserId { get; set; }

        public Guid ReceiverUserId { get; set; }

        public string StatusCode { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime? RespondedAt { get; set; }
    }
}
