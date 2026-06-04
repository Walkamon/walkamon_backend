using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/admin/user")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class UserControllerForAdmin : BaseController
    {
        private readonly IUserService _userService;

        public UserControllerForAdmin(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new
                {
                    Message = "User not found"
                });
            }

            return Ok(user);
        }
        [HttpPatch("{userId:guid}/disable")]
        public async Task<IActionResult> DisableUser(Guid userId)
        {
            await _userService.DisableUserAsync(userId);

            return Ok(new
            {
                Success = true,
                Message = "User disabled successfully"
            });
        }
        [HttpPatch("{userId:guid}/enable")]
        public async Task<IActionResult> EnableUser(Guid userId)
        {
            await _userService.EnableUserAsync(userId);

            return Ok(new
            {
                Success = true,
                Message = "User enabled successfully"
            });
        }
    }
}
