using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Service
{
    public class UserFeedbackService : IUserFeedbackService
    {
        private readonly IGenericRepository<UserFeedback> _feedbackRepository;

        public UserFeedbackService(
            IGenericRepository<UserFeedback> feedbackRepository)
        {
            _feedbackRepository = feedbackRepository;
        }

        public async Task<UserFeedbackResponse> CreateAsync(
            Guid userId,
            CreateUserFeedbackRequest request)
        {
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
