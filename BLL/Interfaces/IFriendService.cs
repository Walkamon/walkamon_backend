using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IFriendService
    {
        Task SendFriendRequestAsync(
            Guid currentUserId,
            SendFriendRequestRequest request);

        Task CancelFriendRequestAsync(
            Guid currentUserId,
            Guid requestId);

        Task RespondFriendRequestAsync(
            Guid currentUserId,
            Guid requestId,
            RespondFriendRequestRequest request);

        Task<IEnumerable<FriendRequestResponse>> GetReceivedRequestsAsync(
            Guid currentUserId);

        Task<IEnumerable<FriendRequestResponse>> GetSentRequestsAsync(
            Guid currentUserId);

        Task<IEnumerable<FriendDto>> GetFriendListAsync(Guid currentUserId);

        Task<IEnumerable<UserSummaryDto>> GetAvailableUsersAsync(Guid currentUserId);
        Task RemoveFriendAsync(
            Guid currentUserId,
            Guid friendId);
    }
}
