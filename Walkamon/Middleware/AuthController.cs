using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : BaseController 
    {
        private readonly IAuthService _authService;

        public AuthController(
            IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginRequest request)
        {
            var result =
                await _authService.LoginAsync(request);

            return Ok(new
            {
                success = true,
                message = "Login success",
                data = result
            });
        }
    }
}