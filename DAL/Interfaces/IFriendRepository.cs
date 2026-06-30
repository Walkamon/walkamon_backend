using DAL.DTO;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IFriendRepository
    {
        Task<List<FriendDto>> GetFriendListAsync(Guid currentUserId);

        Task<List<FriendRequest>> GetReceivedRequestsAsync(Guid userId);

        Task<List<FriendRequest>> GetSentRequestsAsync(Guid userId);

        Task<List<UserSummaryDto>> GetAvailableUsersAsync(Guid currentUserId);

    }
}
