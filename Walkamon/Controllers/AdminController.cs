using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/admin")]
    [Authorize(Roles = "admin")]
    public class AdminController : BaseController
    {
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return Ok(new
            {
                Message = "Welcome Admin",
                UserId = CurrentUserId,
                Email = CurrentEmail,
                Role = CurrentRole
            });
        }
    }
}