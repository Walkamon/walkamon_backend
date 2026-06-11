using Microsoft.AspNetCore.Http;

namespace DAL.DTO;

public class UpdateProfileRequest
{
    public string? Username { get; set; }

    public string? Bio { get; set; }

    public string? Gender { get; set; }

    public DateOnly? Dob { get; set; }

    public IFormFile? Image { get; set; }
}
