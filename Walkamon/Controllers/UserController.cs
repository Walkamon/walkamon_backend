using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/user")]
    [Authorize]
    public class UserController : BaseController
    {
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                UserId = CurrentUserId,
                Email = CurrentEmail,
                Role = CurrentRole
            });
        }
    }
}