namespace DAL.DTO;

public class ResetForgotPasswordRequest
{
    public Guid RequestCode { get; set; }

    public string Otp { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
