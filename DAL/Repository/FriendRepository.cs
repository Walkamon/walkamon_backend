using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DAL.Repository
{
    public class FriendRepository : IFriendRepository
    {
        private readonly WalkamonContext _context;

        public FriendRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<List<FriendDto>> GetFriendListAsync(Guid currentUserId)
        {
            return await _context.Friendships
                .Where(x =>
                    x.UserLowId == currentUserId ||
                    x.UserHighId == currentUserId)

                .Select(x => x.UserLowId == currentUserId
                    ? x.UserHigh.UserProfile
                    : x.UserLow.UserProfile)

                .Select(profile => new FriendDto
                {
                    UserId = profile.UserId,
                    Username = profile.Username,
                    AvatarUrl = profile.AvatarUrl,
                    Bio = profile.Bio,
                    Email = profile.User.Email
                })
                .ToListAsync();
        }

        public async Task<List<FriendRequest>> GetReceivedRequestsAsync(Guid userId)
        {
            return await _context.FriendRequests
                .Include(x => x.SenderUser)
                    .ThenInclude(x => x.UserProfile)
                .Include(x => x.ReceiverUser)
                    .ThenInclude(x => x.UserProfile)
                .Where(x => x.ReceiverUserId == userId)
                .GroupBy(x => x.SenderUserId)
                .Select(g => g
                    .OrderByDescending(x => x.CreatedAt)
                    .First())
                .ToListAsync();
        }

        public async Task<List<FriendRequest>> GetSentRequestsAsync(Guid userId)
        {
            return await _context.FriendRequests
                .Include(x => x.SenderUser)
                    .ThenInclude(x => x.UserProfile)
                .Include(x => x.ReceiverUser)
                    .ThenInclude(x => x.UserProfile)
                .Where(x => x.SenderUserId == userId)
                .GroupBy(x => x.ReceiverUserId)
                .Select(g => g
                    .OrderByDescending(x => x.CreatedAt)
                    .First())
                .ToListAsync();
        }

        public async Task<List<UserSummaryDto>> GetAvailableUsersAsync(Guid currentUserId)
        {
            var friendIds = await _context.Friendships
                .Where(x =>
                    x.UserLowId == currentUserId ||
                    x.UserHighId == currentUserId)
                .Select(x => x.UserLowId == currentUserId
                    ? x.UserHighId
                    : x.UserLowId)
                .ToListAsync();

            var pendingIds = await _context.FriendRequests
                .Where(x =>
                    x.StatusCode == "pending" &&
                    (x.SenderUserId == currentUserId ||
                     x.ReceiverUserId == currentUserId))
                .Select(x => x.SenderUserId == currentUserId
                    ? x.ReceiverUserId
                    : x.SenderUserId)
                .ToListAsync();

            return await _context.Users
                .Where(x =>
                    x.UserId != currentUserId &&
                    x.StatusCode == "active" &&
                    x.Role.RoleName == "User" &&
                    !friendIds.Contains(x.UserId) &&
                    !pendingIds.Contains(x.UserId))
                .Select(x => new UserSummaryDto
                {
                    UserId = x.UserId,
                    Email = x.Email,
                    Username = x.UserProfile!.Username,
                    AvatarUrl = x.UserProfile.AvatarUrl
                })
                .ToListAsync();
        }
    }
}
