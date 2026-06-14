using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IUserFeedbackService
    {
        Task<UserFeedbackResponse> CreateAsync(Guid userId, CreateUserFeedbackRequest request);

        Task<List<UserFeedbackResponse>> GetAllAsync();

        Task<UserFeedbackResponse?> GetByIdAsync(Guid feedbackId);

     

        Task<bool> UpdateStatusAsync(
            Guid feedbackId,
            Guid adminUserId,
            UpdateUserFeedbackRequest request);

        Task<bool> DeleteAsync(Guid feedbackId);
    }
}
