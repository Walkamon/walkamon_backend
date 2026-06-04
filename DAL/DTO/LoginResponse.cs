namespace DAL.DTO
{
    public class LoginResponse
    {
        public Guid UserId { get; set; }

        public string Email { get; set; } = null!;

        public string Role { get; set; } = null!;

        public string Jwt { get; set; } = null!;
    }
}