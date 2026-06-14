using BLL.Interfaces;
using DAL.DTO;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/friends")]
    [Authorize(Roles = "User")]
    [ApiController]
    public class FriendController : BaseController
    {
        private readonly IFriendService _friendService;
       
        public FriendController(IFriendService friendService)
        {
            _friendService = friendService;
        }

        [HttpPost("requests")]
        public async Task<IActionResult> SendRequest(
     SendFriendRequestRequest request)
        {
            await _friendService.SendFriendRequestAsync(
                CurrentUserId,
                request);

            return Ok(new
            {
                message = "Friend request sent successfully."
            });
        }

        [HttpPut("requests/{requestId}")]
        public async Task<IActionResult> Respond(
    Guid requestId,
    RespondFriendRequestRequest request)
        {
            await _friendService.RespondFriendRequestAsync(
                CurrentUserId,
                requestId,
                request);

            return Ok(new
            {
                message = request.IsAccepted
                    ? "Friend request accepted successfully."
                    : "Friend request rejected successfully."
            });
        }

        [HttpDelete("requests/{requestId}")]
        public async Task<IActionResult> Cancel(Guid requestId)
        {
            await _friendService.CancelFriendRequestAsync(
                CurrentUserId,
                requestId);

            return Ok(new
            {
                message = "Friend request cancelled successfully."
            });
        }

        [HttpGet("requests/received")]
        public async Task<IActionResult> Received()
        {
            var result =
                await _friendService.GetReceivedRequestsAsync(CurrentUserId);

            return Ok(new
            {
                message = "Received friend requests retrieved successfully.",
                data = result
            });
        }

        [HttpGet("requests/sent")]
        public async Task<IActionResult> Sent()
        {
            var result =
                await _friendService.GetSentRequestsAsync(CurrentUserId);

            return Ok(new
            {
                message = "Sent friend requests retrieved successfully.",
                data = result
            });
        }
        [HttpGet]
        public async Task<IActionResult> Friends()
        {
            var result =
                await _friendService.GetFriendListAsync(CurrentUserId);

            return Ok(new
            {
                message = "Friend list retrieved successfully.",
                data = result
            });
        }

        [HttpDelete("{friendId}")]
        public async Task<IActionResult> RemoveFriend(Guid friendId)
        {
            await _friendService.RemoveFriendAsync(
                CurrentUserId,
                friendId);

            return Ok(new
            {
                message = "Friend removed successfully."
            });
        }
    }
}
