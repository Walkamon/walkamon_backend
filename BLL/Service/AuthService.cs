//using BLL.Interfaces;
//using DAL.Interfaces;
//using DAL.Model;
//using DAL.DTO;
//using Microsoft.Extensions.Configuration;
//using Microsoft.IdentityModel.Tokens;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using System.Text;
//using BLL.Exception;
//namespace BLL.Service
//{
//    public class AuthService : IAuthService
//    {
//        private readonly IUserRepository _userRepository;
//        private readonly IGenericRepository<Role> _roleRepository;
//        private readonly IConfiguration _configuration;

//        public AuthService(
//            IUserRepository userRepository,
//            IGenericRepository<Role> roleRepository,
//            IConfiguration configuration)
//        {
//            _userRepository = userRepository;
//            _roleRepository = roleRepository;
//            _configuration = configuration;
//        }

    
//        public async Task RegisterAsync(RegisterRequest request)
//        {
//            var checkUsername =
//                await _userRepository.GetByEmailAsync(request.Username);

//            if (checkUsername != null)
//            {
//                throw new ConflictException("Username already exists");
//            }

            
//            var checkEmail =
//                await _userRepository.GetByEmailAsync(request.Email);

//            if (checkEmail != null)
//            {
//                throw new ConflictException("Email already exists");
//            }

//            var role =
//                (await _roleRepository.GetAllAsync())
//                .FirstOrDefault(x => x.RoleName == "USER");

//            if (role == null)
//            {
//                throw new NotFoundException("Default role USER not found");
//            }

//            var user = new User
//            {
//                Username = request.Username,
//                Email = request.Email,
//                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
//                Roles = new List<Role>()
//            };

//            user.Roles.Add(role);

//            await _userRepository.AddAsync(user);
//            await _userRepository.SaveAsync();
//        }

//        public async Task<LoginResponse> LoginAsync(LoginRequest request)
//        {
//            var user =
//                await _userRepository.GetByEmailAsync(request.Username);

//            if (user == null)
//            {
//                throw new NotFoundException("User not found");
//            }

//            bool checkPassword =
//                BCrypt.Net.BCrypt.Verify(request.Password, user.Password);

//            if (!checkPassword)
//            {
//                throw new BadRequestException("Wrong password");
//            }

//            var roles = user.Roles
//                .Select(x => x.RoleName)
//                .ToList();

//            var claims = new List<Claim>
//            {
//                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
//                new Claim(ClaimTypes.Name, user.Username)
//            };

//            foreach (var role in roles)
//            {
//                claims.Add(new Claim(ClaimTypes.Role, role));
//            }

//            var key = new SymmetricSecurityKey(
//                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])
//            );

//            var creds = new SigningCredentials(
//                key,
//                SecurityAlgorithms.HmacSha256
//            );

//            var token = new JwtSecurityToken(
//                claims: claims,
//                expires: DateTime.UtcNow.AddDays(7),
//                signingCredentials: creds
//            );

//            return new LoginResponse
//            {
//                UserId = user.Id,
//                Username = user.Username,
//                Roles = roles,
//                Token = new JwtSecurityTokenHandler().WriteToken(token)
//            };
//        }
//    }
//}