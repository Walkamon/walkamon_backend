using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    }
}
