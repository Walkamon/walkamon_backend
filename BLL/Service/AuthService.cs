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
          
            var user = await _userRepository.GetByEmailAsync(request.Email);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            bool verifyPassword =
                BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash
                );

            if (!verifyPassword)
            {
                throw new BadRequestException("Wrong password");
            }
            Console.WriteLine(user.Email);
            Console.WriteLine(user.Role == null);
            Console.WriteLine(user.Role.RoleName);
            Console.WriteLine(_configuration["Jwt:Key"]);
            Console.WriteLine(_configuration["Jwt:Issuer"]);
            Console.WriteLine(_configuration["Jwt:Audience"]);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier,
                    user.UserId.ToString()),

                new Claim(ClaimTypes.Email,
                    user.Email),

                new Claim(ClaimTypes.Role,
                    user.Role.RoleName)
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