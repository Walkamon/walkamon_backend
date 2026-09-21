using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BLL.Exceptions;
using DAL.Data;
using DAL.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Data;
namespace BLL.Service
{
    public class FriendService : IFriendService
    {
        private readonly WalkamonContext _context;
        private readonly IGenericRepository<FriendRequest> _friendRequestRepository;
        private readonly IGenericRepository<Friendship> _friendshipRepository;
        private readonly IFriendRepository _friendRepository;
        private readonly IPvpPresenceTracker _presenceTracker;
        public FriendService(
     IGenericRepository<FriendRequest> friendRequestRepository,
     IGenericRepository<Friendship> friendshipRepository,
     IFriendRepository friendRepository,
     WalkamonContext context,
     IPvpPresenceTracker? presenceTracker = null)
        {
            _context = context;
            _friendRequestRepository = friendRequestRepository;
            _friendshipRepository = friendshipRepository;
            _friendRepository = friendRepository;
            _presenceTracker = presenceTracker ?? new PvpPresenceTracker();
        }

        public async Task SendFriendRequestAsync(
     Guid currentUserId,
     SendFriendRequestRequest request)
        {
            if (currentUserId == request.ReceiverUserId)
                throw new BadRequestException("Cannot send friend request to yourself.");

            await WithFriendPairAsync(currentUserId, request.ReceiverUserId, async () =>
            {
                var existedRequest = await _friendRequestRepository.AnyAsync(x =>
         (
             (x.SenderUserId == currentUserId &&
              x.ReceiverUserId == request.ReceiverUserId)
             ||
             (x.SenderUserId == request.ReceiverUserId &&
              x.ReceiverUserId == currentUserId)
         )
         && x.StatusCode == "pending");

                if (existedRequest)
                    throw new BadRequestException("Friend request already exists.");

                var lowId = currentUserId.CompareTo(request.ReceiverUserId) < 0
                    ? currentUserId
                    : request.ReceiverUserId;

                var highId = currentUserId.CompareTo(request.ReceiverUserId) > 0
                    ? currentUserId
                    : request.ReceiverUserId;

                var isFriend =
                    await _friendshipRepository.AnyAsync(x =>
                        x.UserLowId == lowId &&
                        x.UserHighId == highId);

                if (isFriend)
                    throw new BadRequestException("Already friends.");

                var entity = new FriendRequest
                {
                    RequestId = Guid.NewGuid(),
                    SenderUserId = currentUserId,
                    ReceiverUserId = request.ReceiverUserId,
                    StatusCode = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _friendRequestRepository.AddAsync(entity);
                await _friendRequestRepository.SaveAsync();
            });
        }

        public async Task CancelFriendRequestAsync(
            Guid currentUserId,
            Guid requestId)
        {
            var pair = await _context.FriendRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestId == requestId)
                ?? throw new NotFoundException("Request not found.");
            if (pair.SenderUserId != currentUserId)
                throw new BadRequestException("Not allowed.");
            await WithFriendPairAsync(pair.SenderUserId, pair.ReceiverUserId, async () =>
            {
                var request =
                    await _friendRequestRepository.GetByIdAsync(requestId);

                if (request == null)
                    throw new NotFoundException("Request not found.");

                if (request.SenderUserId != currentUserId)
                    throw new BadRequestException("Not allowed.");

                if (request.StatusCode != "pending")
                    throw new ConflictException("Friend request has already been processed.");
                request.StatusCode = "cancelled";
                request.RespondedAt = DateTime.UtcNow;

                _friendRequestRepository.Update(request);

                await _friendRequestRepository.SaveAsync();
            });
        }

        public async Task RespondFriendRequestAsync(
            Guid currentUserId,
            Guid requestId,
            RespondFriendRequestRequest request)
        {
            var pair = await _context.FriendRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestId == requestId)
                ?? throw new NotFoundException("Request not found.");
            if (pair.ReceiverUserId != currentUserId)
                throw new NotFoundException("Not allowed.");
            await WithFriendPairAsync(pair.SenderUserId, pair.ReceiverUserId, async () =>
            {
                var friendRequest =
                    await _friendRequestRepository.GetByIdAsync(requestId);

                if (friendRequest == null)
                    throw new NotFoundException("Request not found.");

                if (friendRequest.ReceiverUserId != currentUserId)
                    throw new NotFoundException("Not allowed.");

                if (friendRequest.StatusCode != "pending")
                    throw new ConflictException("Friend request has already been processed.");
                friendRequest.RespondedAt = DateTime.UtcNow;

                if (request.IsAccepted)
                {
                    friendRequest.StatusCode = "accepted";

                    var lowId =
                        friendRequest.SenderUserId.CompareTo(
                            friendRequest.ReceiverUserId) < 0
                            ? friendRequest.SenderUserId
                            : friendRequest.ReceiverUserId;

                    var highId =
                        friendRequest.SenderUserId.CompareTo(
                            friendRequest.ReceiverUserId) > 0
                            ? friendRequest.SenderUserId
                            : friendRequest.ReceiverUserId;

                    var friendship = new Friendship
                    {
                        UserLowId = lowId,
                        UserHighId = highId,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _friendshipRepository.AddAsync(friendship);
                }
                else
                {
                    friendRequest.StatusCode = "rejected";
                }

                _friendRequestRepository.Update(friendRequest);

                await _friendRequestRepository.SaveAsync();
            });
        }

        public async Task<IEnumerable<FriendRequestResponse>>
 GetReceivedRequestsAsync(Guid currentUserId)
        {
            var requests = await _friendRepository
                .GetReceivedRequestsAsync(currentUserId);

            return requests.Select(x => new FriendRequestResponse
            {
                RequestId = x.RequestId,

                User = new UserSummaryDto
                {
                    UserId = x.SenderUserId,
                    Email = x.SenderUser.Email,
                    Username = x.SenderUser.UserProfile?.Username,
                    AvatarUrl = x.SenderUser.UserProfile?.AvatarUrl
                },

                StatusCode = x.StatusCode,
                CreatedAt = x.CreatedAt,
                RespondedAt = x.RespondedAt
            });
        }

        public async Task<IEnumerable<FriendRequestResponse>>
GetSentRequestsAsync(Guid currentUserId)
        {
            var requests = await _friendRepository
                .GetSentRequestsAsync(currentUserId);

            return requests.Select(x => new FriendRequestResponse
            {
                RequestId = x.RequestId,

                User = new UserSummaryDto
                {
                    UserId = x.ReceiverUserId,
                    Email = x.ReceiverUser.Email,
                    Username = x.ReceiverUser.UserProfile?.Username,
                    AvatarUrl = x.ReceiverUser.UserProfile?.AvatarUrl
                },

                StatusCode = x.StatusCode,
                CreatedAt = x.CreatedAt,
                RespondedAt = x.RespondedAt
            });
        }

        public async Task<IEnumerable<UserSummaryDto>>
GetAvailableUsersAsync(Guid currentUserId)
        {
            return await _friendRepository
                .GetAvailableUsersAsync(currentUserId);
        }
        public async Task<IEnumerable<FriendDto>>
   GetFriendListAsync(Guid currentUserId)
        {
            var friends = await _friendRepository.GetFriendListAsync(currentUserId);
            foreach (var friend in friends)
            {
                friend.IsOnline = _presenceTracker.IsOnline(friend.UserId);
                if (!friend.IsOnline)
                    friend.PvpAvailabilityCode = "offline";
            }

            return friends;
        }

        public async Task RemoveFriendAsync(
    Guid currentUserId,
    Guid friendId)
        {
            await WithFriendPairAsync(currentUserId, friendId, async () =>
            {
                var friendship =
                    await _friendshipRepository.FirstOrDefaultAsync(x =>
                        (x.UserLowId == currentUserId &&
                         x.UserHighId == friendId)
                         ||
                        (x.UserLowId == friendId &&
                         x.UserHighId == currentUserId));

                if (friendship == null)
                    throw new NotFoundException("Friendship not found.");

                _friendshipRepository.Delete(friendship);

                await _friendshipRepository.SaveAsync();
            });
        }

        private Task WithFriendPairAsync(Guid first, Guid second, Func<Task> action)
        {
            return _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
            {
                // ponytail: lock user rows in a fixed order; use pair locks if contention grows.
                foreach (var id in new[] { first, second }.Distinct().OrderBy(x => x))
                {
                    if (!await _context.Users.FromSqlInterpolated(
                        $"SELECT * FROM users WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {id}")
                        .AsNoTracking().AnyAsync())
                        throw new NotFoundException("User not found.");
                }
                await action();
            });
        }
    }
}
