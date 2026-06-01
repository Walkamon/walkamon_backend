using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.Service
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<IEnumerable<UserListResponse>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllUsersAsync();

            return users.Select(x => new UserListResponse
            {
                UserId = x.UserId,
                Email = x.Email,
                Username = x.UserProfile?.Username,
                CreatedAt = x.CreatedAt,
                LastLoginAt = x.LastLoginAt,
                StatusCode = x.StatusCode
            });
        }

        public async Task<UserDetailResponse?> GetUserByIdAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdWithProfileAsync(userId);

            if (user == null)
                return null;

            return new UserDetailResponse
            {
                UserId = user.UserId,
                RoleId = user.RoleId,
                Email = user.Email,
                NormalizedEmail = user.NormalizedEmail,
                EmailConfirmed = user.EmailConfirmed,
                StatusCode = user.StatusCode,
                AccessFailedCount = user.AccessFailedCount,
                LockoutEndAt = user.LockoutEndAt,
                LastLoginAt = user.LastLoginAt,
                PasswordChangedAt = user.PasswordChangedAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,

                Profile = user.UserProfile == null
                    ? null
                    : new UserProfileResponse
                    {
                        Username = user.UserProfile.Username,
                        NormalizedUsername = user.UserProfile.NormalizedUsername,
                        DisplayName = user.UserProfile.DisplayName,
                        Bio = user.UserProfile.Bio,
                        AvatarUrl = user.UserProfile.AvatarUrl,
                        LanguageCode = user.UserProfile.LanguageCode,
                        ThemeCode = user.UserProfile.ThemeCode,
                        TimeZoneId = user.UserProfile.TimeZoneId,
                        ProfileVisibilityCode = user.UserProfile.ProfileVisibilityCode,
                        ShowActivityStats = user.UserProfile.ShowActivityStats,
                        AllowFriendRequests = user.UserProfile.AllowFriendRequests,
                        NotificationsEnabled = user.UserProfile.NotificationsEnabled,
                        QuietHourStart = user.UserProfile.QuietHourStart,
                        QuietHourEnd = user.UserProfile.QuietHourEnd,
                        CreatedAt = user.UserProfile.CreatedAt,
                        UpdatedAt = user.UserProfile.UpdatedAt
                    }
            };
        }
    }
}
