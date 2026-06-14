using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/user-feedback")]
    [Authorize(Roles = "User")]
    [ApiController]
    public class FeedbackController : BaseController
    {
        private readonly IUserFeedbackService _service;

        public FeedbackController(
            IUserFeedbackService service)
        {
            _service = service;
        }

      
        [HttpPost]
        public async Task<IActionResult> Create(
            CreateUserFeedbackRequest request)
        {
            var result = await _service.CreateAsync(
                CurrentUserId,
                request);

            return Ok(new
            {
                Data = result,
                Message = "Create feedback Successfully"
            });
        }

       
    }
}
