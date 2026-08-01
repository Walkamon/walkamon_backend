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
namespace BLL.Service
{
    public class FriendService : IFriendService
    {
        private readonly IGenericRepository<FriendRequest> _friendRequestRepository;
        private readonly IGenericRepository<Friendship> _friendshipRepository;
        private readonly IFriendRepository _friendRepository;
        private readonly IPvpPresenceTracker _presenceTracker;
        public FriendService(
     IGenericRepository<FriendRequest> friendRequestRepository,
     IGenericRepository<Friendship> friendshipRepository,
     IFriendRepository friendRepository,
     IPvpPresenceTracker? presenceTracker = null)
        {
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
        }

        public async Task CancelFriendRequestAsync(
            Guid currentUserId,
            Guid requestId)
        {
            var request =
                await _friendRequestRepository.GetByIdAsync(requestId);

            if (request == null)
                throw new NotFoundException("Request not found.");

            if (request.SenderUserId != currentUserId)
                throw new BadRequestException("Not allowed.");

            request.StatusCode = "cancelled";

            _friendRequestRepository.Update(request);

            await _friendRequestRepository.SaveAsync();
        }

        public async Task RespondFriendRequestAsync(
            Guid currentUserId,
            Guid requestId,
            RespondFriendRequestRequest request)
        {
            var friendRequest =
                await _friendRequestRepository.GetByIdAsync(requestId);

            if (friendRequest == null)
                throw new NotFoundException("Request not found.");

            if (friendRequest.ReceiverUserId != currentUserId)
                throw new NotFoundException("Not allowed.");

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
                await _friendshipRepository.SaveAsync();
            }
            else
            {
                friendRequest.StatusCode = "rejected";
            }

            _friendRequestRepository.Update(friendRequest);

            await _friendRequestRepository.SaveAsync();
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
        }
    }
}
