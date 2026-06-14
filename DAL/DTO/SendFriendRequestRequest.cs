using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class SendFriendRequestRequest
    {
        public Guid ReceiverUserId { get; set; }
    }
}
