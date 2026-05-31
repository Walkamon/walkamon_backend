using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BLL.Service
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(
                request.Email
            );

            if (user == null)
            {
                throw new NotFoundException(
                    "User not found"
                );
            }

           
            if (user.StatusCode.Equals(
                "disabled",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Account has been locked"
                );
            }

            
            if (user.LockoutEndAt.HasValue &&
                user.LockoutEndAt > DateTime.UtcNow)
            {
                var remain =
                    (user.LockoutEndAt.Value - DateTime.UtcNow)
                    .Minutes;

                throw new BadRequestException(
                    $"Account is locked. Try again after {remain} minute(s)"
                );
            }

            bool verifyPassword =
                BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash
                );

            if (!verifyPassword)
            {
                user.AccessFailedCount++;

                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEndAt =
                        DateTime.UtcNow.AddMinutes(5);

                     _userRepository.Update(user);
                    await _userRepository.SaveAsync();

                    throw new BadRequestException(
                        "Account locked for 5 minutes because too many failed login attempts"
                    );
                }

                _userRepository.Update(user);
                await _userRepository.SaveAsync();

                throw new BadRequestException(
                    $"Wrong password. Remaining attempts: {5 - user.AccessFailedCount}"
                );
            }

           
            user.AccessFailedCount = 0;
            user.LockoutEndAt = null;
            user.LastLoginAt = DateTime.UtcNow;

             _userRepository.Update(user);
            await _userRepository.SaveAsync();

            var claims = new List<Claim>
    {
        new Claim(
            ClaimTypes.NameIdentifier,
            user.UserId.ToString()
        ),

        new Claim(
            ClaimTypes.Email,
            user.Email
        ),

        new Claim(
            ClaimTypes.Role,
            user.Role.RoleName
        )
    };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]!
                )
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials
            );

            return new LoginResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                Role = user.Role.RoleName,
                Jwt = new JwtSecurityTokenHandler()
                    .WriteToken(token)
            };
        }
    }
}