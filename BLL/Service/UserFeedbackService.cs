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
    public class UserFeedbackService : IUserFeedbackService
    {
        private readonly IGenericRepository<UserFeedback> _feedbackRepository;
        private readonly IEmailSender _emailSender;
        private readonly IGenericRepository<User> _userRepository;
        private readonly IFeedbackRepository _feedback;
        public UserFeedbackService(
            IGenericRepository<UserFeedback> feedbackRepository,
            IGenericRepository<User> userRepository,
            IEmailSender emailSender,
            IFeedbackRepository feedback)
        {
            _feedbackRepository = feedbackRepository;
            _userRepository = userRepository;
            _emailSender = emailSender;
            _feedback = feedback;
        }

        public async Task<UserFeedbackResponse> CreateAsync(
            Guid userId,
            CreateUserFeedbackRequest request)
        {
            var latestFeedback = await _feedback
      .GetLatestFeedbackByUserIdAsync(userId);

            if (latestFeedback != null &&
                latestFeedback.CreatedAt.AddHours(24) > DateTime.UtcNow)
            {
                throw new BadRequestException(
                    "You can only submit feedback once every 24 hours."
                );
            }
            var feedback = new UserFeedback
            {
                UserId = userId,
                FeedbackTypeCode = request.FeedbackTypeCode,
                Content = request.Content,
                StatusCode = "pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _feedbackRepository.AddAsync(feedback);
            await _feedbackRepository.SaveAsync();

            return new UserFeedbackResponse
            {
                FeedbackId = feedback.FeedbackId,
                UserId = feedback.UserId,
                FeedbackTypeCode = feedback.FeedbackTypeCode,
                Content = feedback.Content,
                StatusCode = feedback.StatusCode,
                AdminNote = feedback.AdminNote,
                HandledByUserId = feedback.HandledByUserId,
                HandledAt = feedback.HandledAt,
                CreatedAt = feedback.CreatedAt,
                UpdatedAt = feedback.UpdatedAt
            };
        }

        public async Task<List<UserFeedbackResponse>> GetAllAsync()
        {
            var feedbacks = await _feedbackRepository.GetAllAsync();

            return feedbacks.Select(x => new UserFeedbackResponse
            {
                FeedbackId = x.FeedbackId,
                UserId = x.UserId,
                FeedbackTypeCode = x.FeedbackTypeCode,
                Content = x.Content,
                StatusCode = x.StatusCode,
                AdminNote = x.AdminNote,
                HandledByUserId = x.HandledByUserId,
                HandledAt = x.HandledAt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToList();
        }

        public async Task<UserFeedbackResponse?> GetByIdAsync(Guid feedbackId)
        {
            var feedback = await _feedbackRepository.GetByIdAsync(feedbackId);

            if (feedback == null)
                return null;

            return new UserFeedbackResponse
            {
                FeedbackId = feedback.FeedbackId,
                UserId = feedback.UserId,
                FeedbackTypeCode = feedback.FeedbackTypeCode,
                Content = feedback.Content,
                StatusCode = feedback.StatusCode,
                AdminNote = feedback.AdminNote,
                HandledByUserId = feedback.HandledByUserId,
                HandledAt = feedback.HandledAt,
                CreatedAt = feedback.CreatedAt,
                UpdatedAt = feedback.UpdatedAt
            };
        }

       
        public async Task<bool> UpdateStatusAsync(
            Guid feedbackId,
            Guid adminUserId,
            UpdateUserFeedbackRequest request)
        {
            var feedback = await _feedbackRepository.GetByIdAsync(feedbackId);

            if (feedback == null)
                return false;

            feedback.StatusCode = request.StatusCode;
            feedback.AdminNote = request.AdminNote;
            feedback.UpdatedAt = DateTime.UtcNow;

            if (request.StatusCode == "resolved" ||
                request.StatusCode == "rejected")
            {
                feedback.HandledByUserId = adminUserId;
                feedback.HandledAt = DateTime.UtcNow;
            }

            _feedbackRepository.Update(feedback);
            await _feedbackRepository.SaveAsync();
            if (request.StatusCode == "resolved" ||
    request.StatusCode == "rejected")
            {
                var user = await _userRepository.GetByIdAsync(feedback.UserId);

                if (user != null)
                {
                    var subject = request.StatusCode == "resolved"
                        ? "Walkamon - Feedback Resolved"
                        : "Walkamon - Feedback Rejected";

                    var htmlBody = $@"
        <html>
        <body style='font-family:Arial,sans-serif'>
            <h2>Feedback Update</h2>

            <p>Hello,</p>

            <p>Your feedback has been
               <strong>{request.StatusCode}</strong>.</p>

            <p>
                <strong>Feedback Type:</strong>
                {feedback.FeedbackTypeCode}
            </p>

            <p>
                <strong>Your Feedback:</strong>
            </p>

            <div style='padding:10px;background:#f5f5f5'>
                {feedback.Content}
            </div>

            <p>
                <strong>Admin Note:</strong>
            </p>

            <div style='padding:10px;background:#f5f5f5'>
                {feedback.AdminNote ?? "No additional note."}
            </div>

            <br/>

            <p>
                Thank you for helping improve Walkamon.
            </p>
        </body>
        </html>";

                    await _emailSender.SendEmailAsync(
                        user.Email,
                        subject,
                        htmlBody);
                }
            }
            return true;
        }

        public async Task<bool> DeleteAsync(Guid feedbackId)
        {
            var feedback = await _feedbackRepository.GetByIdAsync(feedbackId);

            if (feedback == null)
                return false;

            _feedbackRepository.Delete(feedback);
            await _feedbackRepository.SaveAsync();

            return true;
        }
    }
}
