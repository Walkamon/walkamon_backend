using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers
{
    [Route("api/user-report")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class ReportControllerForAdmin : BaseController
    {
        private readonly IUserFeedbackService _service;

        public ReportControllerForAdmin(
            IUserFeedbackService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);

            if (result == null)
                return NotFound(new
                {
                    Message = "Feedback not exit"
                });

            return Ok(new
            {
                Data = result,
                Message = "Get feedback Successfully"
            });
        }

     

     

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid id, UpdateUserFeedbackRequest request)
        {
            var success = await _service.UpdateStatusAsync(
                id,
                CurrentUserId,
                request);

            if (!success)
                return NotFound(new
                {
                    Message = "Can't update feedback "
                });

            return Ok(new
            {
                Message = "Update feedback Successfully"
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _service.DeleteAsync(id);

            if (!success)
                return NotFound(new
                {              
                    Message = "Can't delete feedback "
                });

            return Ok(new
            {
                Message = "Delete Feedback Successfully"
            });
        }
    }
}
