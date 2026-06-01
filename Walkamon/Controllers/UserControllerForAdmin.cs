using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/admin/user")]
    [Authorize(Roles = "admin")]
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
    }
}
