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

        public UserSummaryDto User { get; set; } = null!;

        public string StatusCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? RespondedAt { get; set; }
    }
}
