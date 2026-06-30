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

        public string SenderEmail { get; set; } = string.Empty;

        public string? SenderUsername { get; set; }

        public string? SenderAvatarUrl { get; set; }

        public Guid ReceiverUserId { get; set; }

        public string ReceiverEmail { get; set; } = string.Empty;

        public string? ReceiverUsername { get; set; }

        public string? ReceiverAvatarUrl { get; set; }

        public string StatusCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? RespondedAt { get; set; }
    }
}
