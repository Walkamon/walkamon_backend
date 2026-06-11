using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BLL.Exceptions;
namespace BLL.Service
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task DisableUserAsync(Guid userId)
        {
            var user = await _userRepository.GetUserWithRoleAsync(userId);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.Role?.RoleName == "Admin")
            {
                throw new NotFoundException("Cannot disable Admin account");
            }

            user.StatusCode = "disabled";
            user.DeletedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            _userRepository.Update(user);

            await _userRepository.SaveAsync();
        }

        public async Task EnableUserAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            user.StatusCode = "active";
            user.DeletedAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            _userRepository.Update(user);

            await _userRepository.SaveAsync();
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

            return ToUserDetailResponse(user);
        }

        public async Task<UserDetailResponse> UpdateProfileAsync(
            Guid userId,
            UpdateProfileRequest request,
            string? avatarUrl)
        {
            var user = await _userRepository.GetByIdWithProfileAsync(userId);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.UserProfile == null)
            {
                throw new NotFoundException("User profile not found");
            }

            var now = DateTime.UtcNow;

            if (request.Username != null)
            {
                var username = request.Username.Trim();
                if (username.Length is < 3 or > 30)
                {
                    throw new BadRequestException(
                        "Username must be between 3 and 30 characters");
                }

                if (await _userRepository.UsernameExistsAsync(username, userId))
                {
                    throw new ConflictException("Username already exists");
                }

                user.UserProfile.Username = username;
            }

            if (request.Gender != null)
            {
                var gender = request.Gender.Trim().ToLowerInvariant();
                var allowedGenders = new[] { "male", "female", "other" };

                if (!allowedGenders.Contains(gender))
                {
                    throw new BadRequestException(
                        "Gender must be male, female, or other");
                }

                user.UserProfile.Gender = gender;
            }

            if (request.Bio != null)
            {
                var bio = request.Bio.Trim();
                if (bio.Length > 280)
                {
                    throw new BadRequestException(
                        "Bio must not exceed 280 characters");
                }

                user.UserProfile.Bio = string.IsNullOrWhiteSpace(bio)
                    ? null
                    : bio;
            }

            if (request.Dob.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var youngestAllowedDob = today.AddYears(-3);

                if (request.Dob.Value > youngestAllowedDob)
                {
                    throw new BadRequestException(
                        "User must be at least 3 years old");
                }

                user.UserProfile.Dob = request.Dob;
            }

            if (!string.IsNullOrWhiteSpace(avatarUrl))
            {
                user.UserProfile.AvatarUrl = avatarUrl;
            }

            user.UserProfile.UpdatedAt = now;
            user.UpdatedAt = now;

            _userRepository.Update(user);
            await _userRepository.SaveAsync();

            return ToUserDetailResponse(user);
        }

        private static UserDetailResponse ToUserDetailResponse(User user)
        {
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
                        Bio = user.UserProfile.Bio,
                        Gender = user.UserProfile.Gender,
                        Dob = user.UserProfile.Dob,
                        AvatarUrl = user.UserProfile.AvatarUrl,
                        HasSeenStory = user.UserProfile.HasSeenStory,
                        LanguageCode = user.UserProfile.LanguageCode,
                        ThemeCode = user.UserProfile.ThemeCode,
                        TimeZoneId = user.UserProfile.TimeZoneId,
                        ShowActivityStats = user.UserProfile.ShowActivityStats,
                        NotificationsEnabled = user.UserProfile.NotificationsEnabled,
                        CreatedAt = user.UserProfile.CreatedAt,
                        UpdatedAt = user.UserProfile.UpdatedAt
                    }
            };
        }
    }
}
